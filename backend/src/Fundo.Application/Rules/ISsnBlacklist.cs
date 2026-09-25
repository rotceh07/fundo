using Fundo.Domain.Customers;

namespace Fundo.Application.Rules;

public interface ISsnBlacklist
{
    bool Contains(Ssn ssn);
}
