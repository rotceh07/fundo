namespace Fundo.Application.Rules;

public interface IApplicationRule
{
    RuleResult Evaluate(ApplicationRuleContext context);
}
