using Microsoft.Extensions.Logging;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>org-membership: delivery of the "you've been invited" email. The accept endpoint also
/// returns the accept link in its response so an admin can share it directly while a real
/// transactional-email provider is not yet wired (see tasks.md 3.8).</summary>
public interface IInvitationEmailSender
{
    Task SendAsync(string toEmail, string organizationName, string acceptUrl, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default sender: structured log only. There is no existing transactional-email path in this service
/// (operator alerting uses SNS to the operator, not arbitrary recipients), so a real provider (SES)
/// is a deliberate follow-up. Until then the invite link is surfaced to the inviting admin in the API
/// response, so the flow is usable without email.
/// </summary>
public sealed class LoggingInvitationEmailSender : IInvitationEmailSender
{
    private readonly ILogger<LoggingInvitationEmailSender> _logger;

    public LoggingInvitationEmailSender(ILogger<LoggingInvitationEmailSender> logger) => _logger = logger;

    public Task SendAsync(string toEmail, string organizationName, string acceptUrl, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "invitation_email_pending to={ToEmail} org={OrganizationName} acceptUrl={AcceptUrl}",
            toEmail, organizationName, acceptUrl);
        return Task.CompletedTask;
    }
}
