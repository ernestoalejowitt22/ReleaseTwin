namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>plan-tier-gating: thrown when a Free-tier organization attempts to create a project beyond its one-project limit.</summary>
public sealed class ProjectLimitExceededException : Exception
{
    public ProjectLimitExceededException(string message) : base(message)
    {
    }
}
