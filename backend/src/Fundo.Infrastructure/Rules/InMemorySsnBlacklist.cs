using Fundo.Application.Rules;
using Fundo.Domain.Customers;

namespace Fundo.Infrastructure.Rules;

// Blacklist loaded once from configuration. Values go through Ssn.Create so any
// formatting matches, and an invalid configured value fails at startup.
public sealed class InMemorySsnBlacklist : ISsnBlacklist
{
    private readonly HashSet<Ssn> _ssns;

    public InMemorySsnBlacklist(IEnumerable<string> ssns)
    {
        ArgumentNullException.ThrowIfNull(ssns);

        _ssns = ssns.Select(Ssn.Create).ToHashSet();
    }

    public bool Contains(Ssn ssn)
    {
        ArgumentNullException.ThrowIfNull(ssn);

        return _ssns.Contains(ssn);
    }
}
