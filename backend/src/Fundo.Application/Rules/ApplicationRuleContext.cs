using Fundo.Domain.Customers;

namespace Fundo.Application.Rules;

// Carries only the data the current rules need. Add properties here when a new rule needs them.
public sealed record ApplicationRuleContext
{
    private ApplicationRuleContext(string state, Ssn ssn)
    {
        State = state;
        Ssn = ssn;
    }

    public string State { get; }

    public Ssn Ssn { get; }

    public static ApplicationRuleContext Create(string state, Ssn ssn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentNullException.ThrowIfNull(ssn);

        return new ApplicationRuleContext(state.Trim().ToUpperInvariant(), ssn);
    }
}
