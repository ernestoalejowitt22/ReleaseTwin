namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>hosted-project-secrets: thrown when a Free-tier organization attempts to store a project secret.</summary>
public sealed class PaidTierRequiredException : Exception
{
    public PaidTierRequiredException(string message) : base(message)
    {
    }
}
