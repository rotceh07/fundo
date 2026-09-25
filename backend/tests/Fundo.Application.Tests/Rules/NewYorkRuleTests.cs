using Fundo.Application.Rules;
using Fundo.Domain.Customers;

namespace Fundo.Application.Tests.Rules;

public class NewYorkRuleTests
{
    private static readonly Ssn DefaultSsn = Ssn.Create("123-45-6789");

    private readonly NewYorkRule _rule = new();

    [Fact]
    public void Evaluate_WithNewYorkState_DeniesApplication()
    {
        var context = ApplicationRuleContext.Create("NY", DefaultSsn);

        var result = _rule.Evaluate(context);

        Assert.True(result.IsDenied);
        Assert.Equal("Applications from NY are not eligible.", result.Reason);
    }

    [Theory]
    [InlineData("FL")]
    [InlineData("CA")]
    [InlineData("TX")]
    public void Evaluate_WithAllowedState_Passes(string state)
    {
        var context = ApplicationRuleContext.Create(state, DefaultSsn);

        var result = _rule.Evaluate(context);

        Assert.False(result.IsDenied);
    }

    [Fact]
    public void Evaluate_WithNullContext_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _rule.Evaluate(null!));
    }
}
