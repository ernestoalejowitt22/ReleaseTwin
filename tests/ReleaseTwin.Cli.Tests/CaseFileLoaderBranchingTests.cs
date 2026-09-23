using ReleaseTwin.Cli.CaseLoading;
using ReleaseTwin.Core;

namespace ReleaseTwin.Cli.Tests;

public class CaseFileLoaderBranchingTests
{
    private const string Header = """
        id: CASE-B
        oracle:
          locator: t/B
        fixture:
          locator: f.json
        """;

    private static LoadedCase Load(string pipelineYaml)
    {
        var root = Directory.CreateTempSubdirectory("releasetwin-branching-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "fixtures"));
        File.WriteAllText(Path.Combine(root, "fixtures", "f.json"), "{}");
        return CaseFileLoader.ForFixturesRoot(Path.Combine(root, "fixtures"), _ => null)
            .ParseYaml("case.yaml", Header + "\n" + pipelineYaml);
    }

    private static CaseFileException Rejects(string pipelineYaml) => Assert.Throws<CaseFileException>(() => Load(pipelineYaml));

    [Fact]
    public void AChoiceAndAGuardLoadIntoTheCoreModel()
    {
        var loaded = Load("""
            pipeline:
              - operation: http.request
                capture:
                  - name: promoState
                    from: json:$.state
              - kind: "choice"
                when:
                  ref: "promoState"
                  op: "=="
                  value: "applied"
                then:
                  - operation: http.request
                    with:
                      url: https://example.com/applied
                else: []
              - operation: http.request
                when:
                  ref: "{{promoState}}"
                  op: "exists"
            """);

        var pipeline = loaded.Case.Pipeline;
        Assert.Equal(3, pipeline.Count);
        var choice = Assert.IsType<ChoiceStep>(pipeline[1]);
        Assert.Equal(new Condition("promoState", ConditionOperator.Equals, "applied"), choice.When);
        Assert.Single(choice.Then);
        Assert.Empty(choice.Else);
        var guarded = Assert.IsType<PipelineStep>(pipeline[2]);
        Assert.Equal(new Condition("promoState", ConditionOperator.Exists), guarded.When);
    }

    [Fact]
    public void ALinearCaseLoadsUnchanged()
    {
        var loaded = Load("""
            pipeline:
              - operation: a
              - operation: b
                with:
                  x: "{{neverCaptured}}"
            """);

        Assert.All(loaded.Case.Pipeline, e => Assert.IsType<PipelineStep>(e));
        Assert.Null(((PipelineStep)loaded.Case.Pipeline[1]).When);
    }

    [Fact]
    public void ANestedChoiceIsRejected()
    {
        var ex = Rejects("""
            pipeline:
              - operation: a
                capture:
                  - name: s
                    from: json:$.s
              - kind: choice
                when: { ref: s, op: exists }
                then:
                  - kind: choice
                    when: { ref: s, op: exists }
                    then: []
                    else: []
            """);

        Assert.Contains("cannot be nested", ex.Message);
    }

    [Fact]
    public void AnUnknownKindIsRejected()
    {
        Assert.Contains("unknown pipeline entry kind 'loop'", Rejects("""
            pipeline:
              - kind: loop
            """).Message);
    }

    [Fact]
    public void AnUnsupportedOperatorIsRejected()
    {
        Assert.Contains("unsupported 'op' '>'", Rejects("""
            pipeline:
              - operation: a
                capture: [{ name: s, from: "json:$.s" }]
              - kind: choice
                when: { ref: s, op: ">", value: "1" }
                then: []
                else: []
            """).Message);
    }

    [Fact]
    public void AnEqualityConditionWithoutAValueIsRejected()
    {
        Assert.Contains("missing 'value'", Rejects("""
            pipeline:
              - operation: a
                capture: [{ name: s, from: "json:$.s" }]
              - operation: b
                when: { ref: s, op: "==" }
            """).Message);
    }

    [Fact]
    public void AChoiceWithoutWhenIsRejected()
    {
        Assert.Contains("missing 'when'", Rejects("""
            pipeline:
              - kind: choice
                then: []
            """).Message);
    }

    [Fact]
    public void ThenOnAPlainStepIsRejected()
    {
        Assert.Contains("only valid on a 'kind: choice' entry", Rejects("""
            pipeline:
              - operation: a
                then: []
            """).Message);
    }

    [Fact]
    public void AConditionReferencingAnUncapturedNameIsNotInScope()
    {
        Assert.Contains("not in scope", Rejects("""
            pipeline:
              - operation: a
                when: { ref: s, op: exists }
            """).Message);
    }

    [Fact]
    public void ACaptureFromTheSameBranchResolves()
    {
        var loaded = Load("""
            pipeline:
              - operation: a
                capture: [{ name: s, from: "json:$.s" }]
              - kind: choice
                when: { ref: s, op: exists }
                then:
                  - operation: b
                    capture: [{ name: local, from: "json:$.l" }]
                  - operation: c
                    with: { x: "{{local}}" }
                    when: { ref: local, op: exists }
                else: []
            """);

        Assert.IsType<ChoiceStep>(loaded.Case.Pipeline[1]);
    }

    [Fact]
    public void ACaptureFromBeforeTheChoiceResolvesInEitherBranch()
    {
        var loaded = Load("""
            pipeline:
              - operation: a
                capture: [{ name: s, from: "json:$.s" }]
              - kind: choice
                when: { ref: s, op: exists }
                then:
                  - operation: b
                    with: { x: "{{s}}" }
                else:
                  - operation: c
                    with: { x: "{{s}}" }
            """);

        Assert.IsType<ChoiceStep>(loaded.Case.Pipeline[1]);
    }

    [Fact]
    public void ACaptureFromTheSiblingBranchIsNotInScope()
    {
        Assert.Contains("not in scope", Rejects("""
            pipeline:
              - operation: a
                capture: [{ name: s, from: "json:$.s" }]
              - kind: choice
                when: { ref: s, op: exists }
                then:
                  - operation: b
                    capture: [{ name: local, from: "json:$.l" }]
                else:
                  - operation: c
                    with: { x: "{{local}}" }
            """).Message);
    }

    [Fact]
    public void ACaptureFromInsideABranchIsNotInScopeAfterTheJoin()
    {
        Assert.Contains("not in scope", Rejects("""
            pipeline:
              - operation: a
                capture: [{ name: s, from: "json:$.s" }]
              - kind: choice
                when: { ref: s, op: exists }
                then:
                  - operation: b
                    capture: [{ name: local, from: "json:$.l" }]
                else: []
              - operation: c
                with:
                  headers:
                    Authorization: "Bearer {{local}}"
            """).Message);
    }

    [Fact]
    public void AGuardAfterTheJoinOnABranchCaptureIsNotInScope()
    {
        Assert.Contains("not in scope", Rejects("""
            pipeline:
              - operation: a
                capture: [{ name: s, from: "json:$.s" }]
              - kind: choice
                when: { ref: s, op: exists }
                then:
                  - operation: b
                    capture: [{ name: local, from: "json:$.l" }]
                else: []
              - operation: c
                when: { ref: local, op: exists }
            """).Message);
    }
}
