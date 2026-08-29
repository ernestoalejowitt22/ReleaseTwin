using ReleaseTwin.Cli.CaseLoading;

namespace ReleaseTwin.Cli.Tests;

public class CaseFileLoaderEvidenceTests
{
    private static string CreateWorkspace()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-evidence-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "cases"));
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        File.WriteAllText(Path.Combine(root, "fixtures", "f.json"), "{}");
        return root;
    }

    private static LoadedCase Load(string root, string yaml)
    {
        File.WriteAllText(Path.Combine(root, "cases", "case1.yaml"), yaml);
        return new CaseFileLoader(Path.Combine(root, "cases"), Path.Combine(root, "fixtures")).LoadAll().Single();
    }

    [Fact]
    public void AbsentBlockIsInert()
    {
        var loaded = Load(CreateWorkspace(), """
            id: CASE-1
            oracle: { locator: t/1 }
            fixture: { locator: f.json }
            """);

        Assert.True(loaded.Evidence.IsEmpty);
        Assert.Same(EvidenceRules.None, loaded.Evidence);
    }

    [Fact]
    public void BlockParsesAllowlistAndDenylist()
    {
        var loaded = Load(CreateWorkspace(), """
            id: CASE-1
            oracle: { locator: t/1 }
            fixture: { locator: f.json }
            evidence:
              capture:
                - $.order.id
              redact:
                - json_path: $.customer.email
                - header: X-Trace
                - field: ssn
                - selector: "#card-number"
                - region: 10,20,100,40
            """);

        Assert.Equal(new[] { "$.order.id" }, loaded.Evidence.CaptureAllow);
        Assert.Collection(loaded.Evidence.Redact,
            r => { Assert.Equal(EvidenceRedactKind.JsonPath, r.Kind); Assert.Equal("$.customer.email", r.Value); },
            r => { Assert.Equal(EvidenceRedactKind.Header, r.Kind); Assert.Equal("X-Trace", r.Value); },
            r => { Assert.Equal(EvidenceRedactKind.Field, r.Kind); Assert.Equal("ssn", r.Value); },
            r => { Assert.Equal(EvidenceRedactKind.Selector, r.Kind); Assert.Equal("#card-number", r.Value); },
            r => { Assert.Equal(EvidenceRedactKind.Region, r.Kind); Assert.Equal("10,20,100,40", r.Value); });
    }

    [Fact]
    public void RedactRuleWithNoKeyIsRejected()
    {
        var ex = Assert.Throws<CaseFileException>(() => Load(CreateWorkspace(), """
            id: CASE-1
            oracle: { locator: t/1 }
            fixture: { locator: f.json }
            evidence:
              redact:
                - owner: nobody
            """));
        Assert.Contains("exactly one of", ex.Message);
    }

    [Fact]
    public void RedactRuleWithTwoKeysIsRejected()
    {
        var ex = Assert.Throws<CaseFileException>(() => Load(CreateWorkspace(), """
            id: CASE-1
            oracle: { locator: t/1 }
            fixture: { locator: f.json }
            evidence:
              redact:
                - header: X-Trace
                  field: ssn
            """));
        Assert.Contains("exactly one of", ex.Message);
    }
}
