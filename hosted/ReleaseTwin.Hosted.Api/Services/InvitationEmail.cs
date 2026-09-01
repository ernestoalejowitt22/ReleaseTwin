using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.Extensions.Logging;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>org-membership: delivery of the "you've been invited" email. The invite endpoint also
/// returns the accept link in its response, so an admin can share it directly and the flow stays
/// usable when no transactional-email provider is configured or a send fails
/// (company-and-domain-launch: the spec makes email best-effort, never fatal to the invitation).</summary>
public interface IInvitationEmailSender
{
    Task SendAsync(string toEmail, string organizationName, string acceptUrl, CancellationToken cancellationToken = default);
}

/// <summary>
/// Fallback sender: structured log only. Bound when <c>Notifications:FromAddress</c> is not set — local
/// dev, tests, and any deploy that has not yet wired SES. The invite link is surfaced to the inviting
/// admin in the API response, so the flow is fully usable without email.
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

/// <summary>
/// company-and-domain-launch: real invite delivery via Amazon SES v2. Bound when
/// <c>Notifications:FromAddress</c> is present (SES is the same AWS account and deploy path the API
/// already uses — no new vendor, no new secret). A send failure is caught by
/// <see cref="OrganizationMembersService.SendInvitationEmailAsync"/> and never invalidates the
/// invitation, whose accept link is also returned in the API response.
/// </summary>
public sealed class SesInvitationEmailSender : IInvitationEmailSender
{
    private readonly IAmazonSimpleEmailServiceV2 _ses;
    private readonly string _fromAddress;
    private readonly ILogger<SesInvitationEmailSender> _logger;

    public SesInvitationEmailSender(
        IAmazonSimpleEmailServiceV2 ses,
        string fromAddress,
        ILogger<SesInvitationEmailSender> logger)
    {
        _ses = ses;
        _fromAddress = fromAddress;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string organizationName, string acceptUrl, CancellationToken cancellationToken = default)
    {
        var safeOrg = System.Net.WebUtility.HtmlEncode(organizationName);
        var safeUrl = System.Net.WebUtility.HtmlEncode(acceptUrl);

        var request = new SendEmailRequest
        {
            FromEmailAddress = _fromAddress,
            Destination = new Destination { ToAddresses = [toEmail] },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = $"You've been invited to {organizationName} on ReleaseTwin" },
                    Body = new Body
                    {
                        Text = new Content
                        {
                            Data =
                                $"You've been invited to join {organizationName} on ReleaseTwin.\n\n" +
                                $"Accept the invitation:\n{acceptUrl}\n\n" +
                                "If you weren't expecting this, you can ignore this email.",
                        },
                        Html = new Content
                        {
                            Data =
                                $"<p>You've been invited to join <strong>{safeOrg}</strong> on ReleaseTwin.</p>" +
                                $"<p><a href=\"{safeUrl}\">Accept the invitation</a></p>" +
                                "<p>If you weren't expecting this, you can ignore this email.</p>",
                        },
                    },
                },
            },
        };

        var response = await _ses.SendEmailAsync(request, cancellationToken);
        _logger.LogInformation(
            "invitation_email_sent to={ToEmail} org={OrganizationName} messageId={MessageId}",
            toEmail, organizationName, response.MessageId);
    }
}
