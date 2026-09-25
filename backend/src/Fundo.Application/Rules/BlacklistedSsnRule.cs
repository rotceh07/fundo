namespace Fundo.Application.Rules;

public sealed class BlacklistedSsnRule : IApplicationRule
{
    // Neutral on purpose: never include the SSN and avoid revealing the anti fraud rule.
    private const string DenialReason = "SSN is not eligible.";

    private readonly ISsnBlacklist _blacklist;

    public BlacklistedSsnRule(ISsnBlacklist blacklist)
    {
        ArgumentNullException.ThrowIfNull(blacklist);

        _blacklist = blacklist;
    }

    public RuleResult Evaluate(ApplicationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return _blacklist.Contains(context.Ssn)
            ? RuleResult.Deny(DenialReason)
            : RuleResult.Pass();
    }
}
