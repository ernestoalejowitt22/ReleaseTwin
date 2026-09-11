using Newtonsoft.Json.Linq;

namespace ReleaseTwin.Cli.Tests;

/// <summary>
/// local-evidence-viewer (design D7): the fixtures are byte-identical twins of the platform repo's
/// <c>web/src/test/fixtures/</c> copies, and these assertions pin the properties that make them the
/// CLI's *on-disk* form rather than merely valid JSON. Without them a fixture could drift to explicit
/// nulls — which the hosted API accepts but <c>LocalEvidenceWriter</c> never writes — and every other
/// test would still pass.
/// </summary>
public class EvidenceViewerFixtureTests
{
    [Theory]
    [InlineData(EvidenceViewerFixtures.OrdinaryFileName)]
    [InlineData(EvidenceViewerFixtures.FlagProofFileName)]
    public void NeverNullsADocumentFieldTheWriterWouldHaveOmitted(string fileName)
    {
        var document = JObject.Parse(EvidenceViewerFixtures.Read(fileName));

        // NullValueHandling.Ignore governs the document's own fields: every one of these is either
        // present with a value or absent, never present-and-null. (It does *not* reach inside an
        // `adapter` payload — the writer passes that through as a pre-built JToken, so a real
        // document can carry adapter-internal nulls. Nothing here depends on that either way.)
        foreach (var key in new[] { "caseId", "oracleLocator", "legs", "redactionNote" })
        {
            Assert.False(document[key]?.Type == JTokenType.Null, $"'{key}' must be absent or valued, never null");
        }

        foreach (var leg in Legs(fileName))
        {
            Assert.False(leg["leg"]?.Type == JTokenType.Null, "'leg' must be absent or valued, never null");
            Assert.False(leg["steps"]?.Type == JTokenType.Null, "'steps' must be absent or valued, never null");
        }

        foreach (var step in Steps(fileName))
        {
            foreach (var key in new[] { "index", "operationName", "outcome", "durationMs", "assertion", "adapter", "screenshots" })
            {
                Assert.False(step[key]?.Type == JTokenType.Null, $"step '{key}' must be absent or valued, never null");
            }
        }
    }

    [Theory]
    [InlineData(EvidenceViewerFixtures.OrdinaryFileName)]
    [InlineData(EvidenceViewerFixtures.FlagProofFileName)]
    public void CarriesNoNullAtAllSoDriftTowardTheNullTolerantFormFailsHere(string fileName)
    {
        // Stricter than the rule above and deliberately so: these fixtures are hand-held twins, and
        // the cheapest way for one to drift is for someone to "fill in" an absent key with null the
        // way the hosted API would accept. A flat text check catches that at any depth.
        Assert.DoesNotContain("null", EvidenceViewerFixtures.Read(fileName));
    }

    [Theory]
    [InlineData(EvidenceViewerFixtures.OrdinaryFileName)]
    [InlineData(EvidenceViewerFixtures.FlagProofFileName)]
    public void HasAtLeastOneStepMissingEachOptionalKey(string fileName)
    {
        var steps = Steps(fileName);

        foreach (var key in new[] { "assertion", "adapter", "screenshots" })
        {
            Assert.True(
                steps.Any(step => step[key] is null),
                $"{fileName} must keep at least one step with '{key}' absent, or it stops pinning absent-key tolerance");
            Assert.True(
                steps.Any(step => step[key] is not null),
                $"{fileName} must keep at least one step that carries '{key}'");
        }
    }

    [Theory]
    [InlineData(EvidenceViewerFixtures.OrdinaryFileName)]
    [InlineData(EvidenceViewerFixtures.FlagProofFileName)]
    public void UsesTheCamelCaseKeysTheWriterProduces(string fileName)
    {
        var document = JObject.Parse(EvidenceViewerFixtures.Read(fileName));

        Assert.Equal(
            new[] { "caseId", "oracleLocator", "legs", "redactionNote" },
            document.Properties().Select(p => p.Name));
        foreach (var step in Steps(fileName))
        {
            Assert.NotNull(step["index"]);
            Assert.NotNull(step["operationName"]);
            Assert.NotNull(step["outcome"]);
            Assert.NotNull(step["durationMs"]);
        }
    }

    [Fact]
    public void TheOrdinaryDocumentsSingleLegIsUnnamed()
    {
        var legs = Legs(EvidenceViewerFixtures.OrdinaryFileName);

        var leg = Assert.Single(legs);
        // Absent, not empty-string: the redactor omits the name for a non-flag-proof case.
        Assert.Null(leg["leg"]);
    }

    [Fact]
    public void TheFlagProofDocumentsLegsAreKnownBadThenKnownGood()
    {
        var legs = Legs(EvidenceViewerFixtures.FlagProofFileName);

        Assert.Equal(new[] { "known-bad", "known-good" }, legs.Select(l => (string?)l["leg"]));
    }

    [Theory]
    [InlineData(EvidenceViewerFixtures.OrdinaryFileName)]
    [InlineData(EvidenceViewerFixtures.FlagProofFileName)]
    public void CarriesNoCredentialTokenOrUnredactedResponseBody(string fileName)
    {
        var raw = EvidenceViewerFixtures.Read(fileName);

        // Every authorization header in these files is the CLI's own mask value.
        foreach (var authorization in JObject.Parse(raw).SelectTokens("$..headers.authorization"))
        {
            Assert.Equal("«redacted»", (string?)authorization);
        }

        foreach (var forbidden in new[] { "Bearer ", "sk_", "ghp_", "AKIA", "password" })
        {
            Assert.DoesNotContain(forbidden, raw, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(EvidenceViewerFixtures.OrdinaryFileName)]
    [InlineData(EvidenceViewerFixtures.FlagProofFileName)]
    public void UsesTheRedactorsOwnRedactionNote(string fileName)
    {
        var document = JObject.Parse(EvidenceViewerFixtures.Read(fileName));

        Assert.Equal(Cli.Evidence.EvidenceRedactor.RedactionNote, (string?)document["redactionNote"]);
    }

    private static IEnumerable<JToken> Legs(string fileName) =>
        JObject.Parse(EvidenceViewerFixtures.Read(fileName))["legs"]!.Children();

    private static List<JToken> Steps(string fileName) =>
        Legs(fileName).SelectMany(leg => leg["steps"]!.Children()).ToList();
}
