using Fundo.Application.Rules;
using Fundo.Domain.Customers;

namespace Fundo.Application.Tests.Rules;

internal sealed class FakeSsnBlacklist : ISsnBlacklist
{
    private readonly HashSet<Ssn> _values;

    public FakeSsnBlacklist(params Ssn[] values)
    {
        _values = values.ToHashSet();
    }

    public bool Contains(Ssn ssn) => _values.Contains(ssn);
}
