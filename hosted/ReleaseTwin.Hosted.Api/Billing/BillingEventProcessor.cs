using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Billing;

public enum BillingEventOutcome
{
    /// <summary>Signature verified, event applied (or a recognised no-op) and recorded. Return 200.</summary>
    Processed,

    /// <summary>Already in the dedupe store — a duplicate delivery. Return 200, nothing changed.</summary>
    Duplicate,

    /// <summary>The referenced organization could not be resolved. Return non-2xx so Polar redelivers.</summary>
    OrganizationNotFound,
}

/// <summary>
/// billing (design.md D2/D3): the only writer of billing-driven tier + status changes. Maps a Polar
/// subscription-lifecycle event to an intent, applies it via <see cref="ProvisioningService.SetTierAsync"/>
/// + <see cref="IOrganizationRepository.SetBillingAsync"/> (both idempotent "set state X"), then records
/// the event. A failure before the record step throws — the caller returns non-2xx and Polar redelivers.
/// </summary>
public sealed class BillingEventProcessor
{
    private readonly ProvisioningService _provisioning;
    private readonly IOrganizationRepository _organizations;
    private readonly ProcessedBillingEventRepository _processedEvents;
    private readonly ILogger<BillingEventProcessor> _logger;

    public BillingEventProcessor(
        ProvisioningService provisioning,
        IOrganizationRepository organizations,
        ProcessedBillingEventRepository processedEvents,
        ILogger<BillingEventProcessor> logger)
    {
        _provisioning = provisioning;
        _organizations = organizations;
        _processedEvents = processedEvents;
        _logger = logger;
    }

    private enum Intent { Activate, PastDue, Cancel, Ignore }

    public async Task<BillingEventOutcome> ProcessAsync(string providerEventId, string body, CancellationToken cancellationToken = default)
    {
        if (await _processedEvents.HasProcessedAsync(providerEventId, cancellationToken))
        {
            return BillingEventOutcome.Duplicate;
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var type = root.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
        var data = root.TryGetProperty("data", out var d) ? d : default;

        var intent = ResolveIntent(type, data);
        if (intent == Intent.Ignore)
        {
            _logger.LogInformation("Billing webhook {EventId} type '{Type}' has no mapped intent — recorded, no-op.", providerEventId, type);
            await _processedEvents.MarkProcessedAsync(providerEventId, type, cancellationToken);
            return BillingEventOutcome.Processed;
        }

        var organizationId = ResolveOrganizationId(data);
        if (organizationId is null)
        {
            _logger.LogWarning("Billing webhook {EventId} type '{Type}' resolved intent {Intent} but no organization id.", providerEventId, type, intent);
            return BillingEventOutcome.OrganizationNotFound;
        }

        var organization = await _organizations.GetAsync(organizationId.Value, cancellationToken);
        if (organization is null)
        {
            return BillingEventOutcome.OrganizationNotFound;
        }

        var since = ResolveOccurredAt(root, data);
        var subscriptionId = GetString(data, "id");
        var customerId = GetString(data, "customer_id") ?? organization.PolarCustomerId;
        var cadence = ResolveCadence(data) ?? organization.BillingCadence;

        switch (intent)
        {
            case Intent.Activate:
                await _provisioning.SetTierAsync(organizationId.Value, PlanTier.Team, cancellationToken);
                await _organizations.SetBillingAsync(organizationId.Value, BillingStatus.Active, since, cadence, customerId, subscriptionId, cancellationToken);
                break;

            case Intent.PastDue:
                await _organizations.SetBillingAsync(organizationId.Value, BillingStatus.PastDue, since, cadence, customerId, subscriptionId ?? organization.PolarSubscriptionId, cancellationToken);
                break;

            case Intent.Cancel:
                await _organizations.SetBillingAsync(organizationId.Value, BillingStatus.Canceled, since, cadence, customerId, subscriptionId ?? organization.PolarSubscriptionId, cancellationToken);
                break;
        }

        await _processedEvents.MarkProcessedAsync(providerEventId, type, cancellationToken);
        _logger.LogInformation("Billing webhook {EventId} type '{Type}' applied intent {Intent} to org {OrgId}.", providerEventId, type, intent, organizationId);
        return BillingEventOutcome.Processed;
    }

    private static Intent ResolveIntent(string type, JsonElement data)
    {
        var status = GetString(data, "status");

        if (type is "subscription.active" or "subscription.created" or "subscription.uncanceled" or "order.paid")
        {
            return Intent.Activate;
        }

        if (type is "subscription.canceled" or "subscription.revoked")
        {
            return Intent.Cancel;
        }

        if (type is "subscription.past_due")
        {
            return Intent.PastDue;
        }

        // subscription.updated (and any other status-carrying event): drive off the status.
        return status switch
        {
            "active" => Intent.Activate,
            "past_due" => Intent.PastDue,
            "canceled" or "unpaid" or "incomplete_expired" => Intent.Cancel,
            _ => Intent.Ignore,
        };
    }

    private static Guid? ResolveOrganizationId(JsonElement data)
    {
        foreach (var container in new[] { data, GetProperty(data, "metadata"), GetProperty(data, "subscription") })
        {
            var raw = GetString(container, "organization_id") ?? GetString(GetProperty(container, "metadata"), "organization_id");
            if (Guid.TryParse(raw, out var id))
            {
                return id;
            }
        }

        return null;
    }

    private static BillingCadence? ResolveCadence(JsonElement data)
    {
        var interval = GetString(data, "recurring_interval")
            ?? GetString(GetProperty(data, "price"), "recurring_interval");
        return interval switch
        {
            "month" or "monthly" => BillingCadence.Monthly,
            "year" or "annual" or "yearly" => BillingCadence.Annual,
            _ => null,
        };
    }

    private static DateTimeOffset ResolveOccurredAt(JsonElement root, JsonElement data)
    {
        foreach (var candidate in new[]
        {
            GetString(data, "modified_at"),
            GetString(data, "started_at"),
            GetString(data, "created_at"),
            GetString(root, "created_at"),
        })
        {
            if (DateTimeOffset.TryParse(candidate, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            {
                return parsed;
            }
        }

        return DateTimeOffset.UtcNow;
    }

    private static JsonElement GetProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;

    private static string? GetString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
