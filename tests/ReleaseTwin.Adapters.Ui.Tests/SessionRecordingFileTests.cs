using ReleaseTwin.Adapters.Ui;

namespace ReleaseTwin.Adapters.Ui.Tests;

/// <summary>
/// local-evidence-viewer (design D4): the recorder names a session recording and the viewer looks it
/// up again by the same rule. Pinned here so the two cannot drift apart — a rename on one side with
/// no matching rename on the other would silently stop matching recordings to cases.
/// </summary>
public class SessionRecordingFileTests
{
    [Theory]
    [InlineData("CASE-1", "CASE-1")]
    [InlineData("checkout.v2_final", "checkout.v2_final")]
    [InlineData("orders/CASE 1", "orders-CASE-1")]
    [InlineData("a:b*c?d", "a-b-c-d")]
    public void MapsEveryCharacterOutsideTheSafeSetToADash(string caseId, string expected)
    {
        Assert.Equal(expected, SessionRecordingFile.Sanitize(caseId));
    }

    [Fact]
    public void BuildsTheRecordingPathTheRecorderWrites()
    {
        Assert.Equal(
            Path.Combine("videos", "orders-CASE-1.webm"),
            SessionRecordingFile.PathFor("videos", "orders/CASE-1"));
    }
}
