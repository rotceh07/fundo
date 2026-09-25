using Fundo.Application.Rules;
using Fundo.Domain.Customers;

namespace Fundo.Application.Tests.Rules;

public class RuleEngineTests
{
    private static readonly Ssn AllowedSsn = Ssn.Create("123-45-6789");
    private static readonly Ssn BlacklistedSsn = Ssn.Create("111-11-1111");

    private static readonly ApplicationRuleContext DefaultContext =
        ApplicationRuleContext.Create("FL", AllowedSsn);

    [Fact]
    public void Evaluate_WithNoRules_ReturnsPass()
    {
        var engine = new RuleEngine([]);

        var result = engine.Evaluate(DefaultContext);

        Assert.False(result.IsDenied);
    }

    [Fact]
    public void Evaluate_WhenAllRulesPass_ReturnsPass()
    {
        var engine = new RuleEngine([new StubRule(RuleResult.Pass()), new StubRule(RuleResult.Pass())]);

        var result = engine.Evaluate(DefaultContext);

        Assert.False(result.IsDenied);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Evaluate_WhenOneRuleDenies_ReturnsThatDenial()
    {
        var engine = new RuleEngine([new StubRule(RuleResult.Pass()), new StubRule(RuleResult.Deny("Reason B"))]);

        var result = engine.Evaluate(DefaultContext);

        Assert.True(result.IsDenied);
        Assert.Equal("Reason B", result.Reason);
    }

    [Fact]
    public void Evaluate_WhenSeveralRulesDeny_ReturnsFirstDenial()
    {
        var engine = new RuleEngine([new StubRule(RuleResult.Deny("Reason A")), new StubRule(RuleResult.Deny("Reason B"))]);

        var result = engine.Evaluate(DefaultContext);

        Assert.Equal("Reason A", result.Reason);
    }

    [Fact]
    public void Evaluate_WhenRuleDenies_StopsEvaluatingRemainingRules()
    {
        var denyingRule = new StubRule(RuleResult.Deny("Reason A"));
        var laterRule = new StubRule(RuleResult.Pass());
        var engine = new RuleEngine([denyingRule, laterRule]);

        engine.Evaluate(DefaultContext);

        Assert.True(denyingRule.WasEvaluated);
        Assert.False(laterRule.WasEvaluated);
    }

    [Fact]
    public void Evaluate_WithCustomRule_UsesRuleWithoutEngineChanges()
    {
        var engine = new RuleEngine([new NewYorkRule(), new AlwaysDenyRule()]);

        var result = engine.Evaluate(DefaultContext);

        Assert.True(result.IsDenied);
        Assert.Equal(AlwaysDenyRule.Reason, result.Reason);
    }

    [Fact]
    public void Constructor_WithNullRules_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RuleEngine(null!));
    }

    [Fact]
    public void Evaluate_WithNullContext_ThrowsArgumentNullException()
    {
        var engine = new RuleEngine([]);

        Assert.Throws<ArgumentNullException>(() => engine.Evaluate(null!));
    }

    [Theory]
    [InlineData("FL", "123-45-6789", false, null)]
    [InlineData("NY", "123-45-6789", true, "Applications from NY are not eligible.")]
    [InlineData("FL", "111-11-1111", true, "SSN is not eligible.")]
    [InlineData("NY", "111-11-1111", true, "Applications from NY are not eligible.")]
    public void Evaluate_WithChallengeRules_ReturnsExpectedDecision(
        string state,
        string ssn,
        bool expectedDenied,
        string? expectedReason)
    {
        var engine = new RuleEngine(
        [
            new NewYorkRule(),
            new BlacklistedSsnRule(new FakeSsnBlacklist(BlacklistedSsn))
        ]);

        var result = engine.Evaluate(ApplicationRuleContext.Create(state, Ssn.Create(ssn)));

        Assert.Equal(expectedDenied, result.IsDenied);
        Assert.Equal(expectedReason, result.Reason);
    }

    private sealed class StubRule(RuleResult result) : IApplicationRule
    {
        public bool WasEvaluated { get; private set; }

        public RuleResult Evaluate(ApplicationRuleContext context)
        {
            WasEvaluated = true;
            return result;
        }
    }

    private sealed class AlwaysDenyRule : IApplicationRule
    {
        public const string Reason = "Denied by a rule the engine never knew about.";

        public RuleResult Evaluate(ApplicationRuleContext context) => RuleResult.Deny(Reason);
    }
}
