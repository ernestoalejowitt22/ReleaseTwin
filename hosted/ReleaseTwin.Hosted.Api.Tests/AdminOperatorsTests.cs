using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

public class AdminOperatorsTests
{
    [Fact]
    public void Empty_or_null_config_means_nobody_is_an_operator()
    {
        Assert.False(new AdminOperators((string?)null).Any);
        Assert.False(new AdminOperators("").IsOperator("user_123"));
        Assert.False(new AdminOperators("   ").IsOperator("user_123"));
    }

    [Fact]
    public void Comma_space_and_newline_separated_ids_all_parse()
    {
        var operators = new AdminOperators("user_a, user_b\nuser_c\tuser_d");

        Assert.True(operators.IsOperator("user_a"));
        Assert.True(operators.IsOperator("user_b"));
        Assert.True(operators.IsOperator("user_c"));
        Assert.True(operators.IsOperator("user_d"));
        Assert.False(operators.IsOperator("user_e"));
    }

    [Fact]
    public void Operator_match_is_case_sensitive_and_null_safe()
    {
        var operators = new AdminOperators("user_ABC");

        Assert.True(operators.IsOperator("user_ABC"));
        Assert.False(operators.IsOperator("user_abc"));
        Assert.False(operators.IsOperator(null));
    }
}
