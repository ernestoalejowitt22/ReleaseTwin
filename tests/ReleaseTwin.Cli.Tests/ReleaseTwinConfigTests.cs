using ReleaseTwin.Cli;

namespace ReleaseTwin.Cli.Tests;

public class ReleaseTwinConfigTests
{
    [Fact]
    public void No_adapters_key_means_auto_detection()
    {
        var c = ReleaseTwinConfig.Parse("# just a comment\n", "x");

        Assert.Null(c.Adapters);
        Assert.True(c.Considers("azure-devops"));
        Assert.False(c.Requires("azure-devops"));
    }

    [Fact]
    public void Adapters_list_is_authoritative_but_http_is_always_considered()
    {
        // A list that omits http entirely — http is still considered.
        var c = ReleaseTwinConfig.Parse("adapters:\n  - launchdarkly\n", "x");

        Assert.Equal(new[] { "launchdarkly" }, c.Adapters);
        Assert.True(c.Considers("http"));
        Assert.False(c.Requires("http"));
        Assert.True(c.Considers("launchdarkly"));
        Assert.False(c.Considers("azure-devops"));
    }

    [Theory]
    [InlineData("adapters:\n  - AZURE-DEVOPS\n", "azure-devops")]
    [InlineData("adapters: [ launchdarkly ]\n", "launchdarkly")]
    public void Names_are_case_insensitive_and_trimmed(string yaml, string name)
    {
        var c = ReleaseTwinConfig.Parse(yaml, "x");
        Assert.True(c.Requires(name));
        Assert.True(c.Considers(name));
    }

    [Fact]
    public void Unknown_adapter_name_is_a_hard_error()
    {
        var ex = Assert.Throws<ReleaseTwinConfigException>(
            () => ReleaseTwinConfig.Parse("adapters:\n  - http\n  - kafka\n", "releasetwin.yaml"));
        Assert.Contains("kafka", ex.Message);
        Assert.Contains("releasetwin.yaml", ex.Message);
    }

    [Fact]
    public void Malformed_yaml_is_a_hard_error()
    {
        var ex = Assert.Throws<ReleaseTwinConfigException>(
            () => ReleaseTwinConfig.Parse("adapters:\n  - http\n    bad: indent\n  -\n:::", "releasetwin.yaml"));
        Assert.Contains("releasetwin.yaml", ex.Message);
    }

    [Fact]
    public void LoadFor_finds_the_file_in_the_cases_directorys_parent()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-config-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "cases"));
        File.WriteAllText(Path.Combine(root, "releasetwin.yaml"), "adapters:\n  - http\n");

        var c = ReleaseTwinConfig.LoadFor(Path.Combine(root, "cases"));

        Assert.Equal(new[] { "http" }, c.Adapters);
    }

    [Fact]
    public void LoadFor_returns_auto_detection_when_absent()
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-config-none-").FullName;
        var c = ReleaseTwinConfig.LoadFor(Path.Combine(root, "cases"));
        Assert.Null(c.Adapters);
    }
}
