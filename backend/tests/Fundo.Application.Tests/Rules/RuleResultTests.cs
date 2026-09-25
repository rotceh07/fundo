using Fundo.Application.Rules;

namespace Fundo.Application.Tests.Rules;

public class RuleResultTests
{
    [Fact]
    public void Pass_ReturnsNotDeniedResultWithoutReason()
    {
        var result = RuleResult.Pass();

        Assert.False(result.IsDenied);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Deny_WithReason_ReturnsDeniedResultWithReason()
    {
        var result = RuleResult.Deny("Not eligible.");

        Assert.True(result.IsDenied);
        Assert.Equal("Not eligible.", result.Reason);
    }

    [Fact]
    public void Deny_WithNullReason_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleResult.Deny(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Deny_WithEmptyReason_ThrowsArgumentException(string reason)
    {
        Assert.Throws<ArgumentException>(() => RuleResult.Deny(reason));
    }
}
