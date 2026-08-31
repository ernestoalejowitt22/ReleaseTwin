using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Plans;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Endpoints;

/// <summary>
/// run-notifications: per-project outbound notification targets. ClerkJwt (web session) only,
/// admin-gated (<see cref="OrgCapability.ManageNotifications"/>), and further gated on the org's
/// <c>runNotifications</c> entitlement (Team+). On save the URL is validated: HTTPS only, and it must
/// not resolve to a private / loopback / link-local address.
/// </summary>
public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects/{projectId:guid}/notification-targets")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes("ClerkJwt"));

        group.MapGet("/", async (Guid projectId, INotificationTargetRepository targets, IProjectRepository projects,
            IOrganizationRepository organizations, IEntitlementService entitlements, CurrentOrganizationAccessor currentOrg) =>
        {
            var (org, error) = await GateAsync(projectId, currentOrg, projects, organizations, entitlements);
            if (error is not null)
            {
                return error;
            }

            var list = await targets.ListByProjectAsync(projectId);
            return Results.Ok(list.Select(ToView));
        });

        group.MapPost("/", async (Guid projectId, CreateNotificationTargetRequest request, INotificationTargetRepository targets,
            IProjectRepository projects, IOrganizationRepository organizations, IEntitlementService entitlements, CurrentOrganizationAccessor currentOrg,
            Func<string, System.Net.IPAddress[]> resolveHost) =>
        {
            var (org, error) = await GateAsync(projectId, currentOrg, projects, organizations, entitlements);
            if (error is not null)
            {
                return error;
            }

            if (!Enum.TryParse<NotificationTargetKind>(request?.Kind, ignoreCase: true, out var kind))
            {
                return Results.BadRequest(new { error = "invalid-kind" });
            }

            if (!OutboundUrlValidator.IsAllowed(request!.Url, out var reason, resolveHost))
            {
                return Results.BadRequest(new { error = "invalid-url", detail = reason });
            }

            var target = new NotificationTarget
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Kind = kind,
                Url = request.Url!,
                Enabled = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await targets.PutAsync(target);
            return Results.Created($"/api/projects/{projectId}/notification-targets/{target.Id}", ToView(target));
        });

        group.MapPatch("/{targetId:guid}", async (Guid projectId, Guid targetId, UpdateNotificationTargetRequest request,
            INotificationTargetRepository targets, IProjectRepository projects, IOrganizationRepository organizations,
            IEntitlementService entitlements, CurrentOrganizationAccessor currentOrg) =>
        {
            var (org, error) = await GateAsync(projectId, currentOrg, projects, organizations, entitlements);
            if (error is not null)
            {
                return error;
            }

            var target = await targets.GetAsync(projectId, targetId);
            if (target is null)
            {
                return Results.NotFound();
            }

            if (request?.Enabled is { } enabled)
            {
                target.Enabled = enabled;
                await targets.PutAsync(target);
            }

            return Results.Ok(ToView(target));
        });

        group.MapDelete("/{targetId:guid}", async (Guid projectId, Guid targetId, INotificationTargetRepository targets,
            IProjectRepository projects, IOrganizationRepository organizations, IEntitlementService entitlements, CurrentOrganizationAccessor currentOrg) =>
        {
            var (org, error) = await GateAsync(projectId, currentOrg, projects, organizations, entitlements);
            if (error is not null)
            {
                return error;
            }

            await targets.DeleteAsync(projectId, targetId);
            return Results.NoContent();
        });
    }

    private static async Task<(Organization? Org, IResult? Error)> GateAsync(
        Guid projectId, CurrentOrganizationAccessor currentOrg, IProjectRepository projects,
        IOrganizationRepository organizations, IEntitlementService entitlements)
    {
        var orgId = currentOrg.Require(OrgCapability.ManageNotifications);

        if (await projects.GetAsync(orgId, projectId) is null)
        {
            return (null, Results.Forbid());
        }

        var organization = await organizations.GetAsync(orgId);
        if (!entitlements.For(organization).RunNotifications)
        {
            return (organization, Results.Json(
                new { error = "entitlement-required", entitlement = "runNotifications" },
                statusCode: StatusCodes.Status403Forbidden));
        }

        return (organization, null);
    }

    private static NotificationTargetView ToView(NotificationTarget t) =>
        new(t.Id, t.Kind.ToString(), t.Url, t.Enabled, t.LastOutcome, t.LastAttemptAt);
}

public sealed record CreateNotificationTargetRequest(string? Kind, string? Url);
public sealed record UpdateNotificationTargetRequest(bool? Enabled);
public sealed record NotificationTargetView(Guid Id, string Kind, string Url, bool Enabled, string? LastOutcome, DateTimeOffset? LastAttemptAt);
