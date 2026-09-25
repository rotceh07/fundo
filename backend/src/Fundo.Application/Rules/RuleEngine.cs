namespace Fundo.Application.Rules;

public sealed class RuleEngine
{
    private readonly IApplicationRule[] _rules;

    public RuleEngine(IEnumerable<IApplicationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        _rules = rules.ToArray();
    }

    // Rules run in the order they were provided and the first denial wins,
    // so that order also decides which reason is reported.
    public RuleResult Evaluate(ApplicationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var rule in _rules)
        {
            var result = rule.Evaluate(context);

            if (result.IsDenied)
            {
                return result;
            }
        }

        return RuleResult.Pass();
    }
}
