using Fundo.Application.Rules;
using Fundo.Domain.Customers;

namespace Fundo.Application.Tests.Rules;

public class BlacklistedSsnRuleTests
{
    private readonly BlacklistedSsnRule _rule = new(new FakeSsnBlacklist(Ssn.Create("111111111")));

    [Fact]
    public void Evaluate_WithBlacklistedSsn_DeniesApplication()
    {
        var context = ApplicationRuleContext.Create("FL", Ssn.Create("111-11-1111"));

        var result = _rule.Evaluate(context);

        Assert.True(result.IsDenied);
        Assert.Equal("SSN is not eligible.", result.Reason);
    }

    [Fact]
    public void Evaluate_WithAllowedSsn_Passes()
    {
        var context = ApplicationRuleContext.Create("FL", Ssn.Create("123-45-6789"));

        var result = _rule.Evaluate(context);

        Assert.False(result.IsDenied);
    }

    [Fact]
    public void Evaluate_WithBlacklistedSsn_DoesNotExposeSsnInReason()
    {
        var context = ApplicationRuleContext.Create("FL", Ssn.Create("111-11-1111"));

        var result = _rule.Evaluate(context);

        Assert.NotNull(result.Reason);
        Assert.DoesNotContain(context.Ssn.Value, result.Reason);
        Assert.DoesNotContain("111-11-1111", result.Reason);
    }

    [Fact]
    public void Constructor_WithNullBlacklist_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BlacklistedSsnRule(null!));
    }

    [Fact]
    public void Evaluate_WithNullContext_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _rule.Evaluate(null!));
    }
}
