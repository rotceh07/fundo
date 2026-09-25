namespace Fundo.Application.Rules;

public sealed class NewYorkRule : IApplicationRule
{
    private const string DeniedState = "NY";
    private const string DenialReason = "Applications from NY are not eligible.";

    // State arrives already normalized by ApplicationRuleContext.
    public RuleResult Evaluate(ApplicationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.State == DeniedState
            ? RuleResult.Deny(DenialReason)
            : RuleResult.Pass();
    }
}
