using System.Text;

namespace Fundo.Application.Applications.Submit;

public sealed record SubmitApplicationCommand(
    string FirstName,
    string LastName,
    string Address,
    string State,
    string CompanyName,
    decimal RequestedAmount,
    string Ssn)
{
    // Keeps the SSN out of the generated ToString as a safety net.
    // It does not replace the rule of never logging the whole command.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"FirstName = {FirstName}, LastName = {LastName}, Address = {Address}, State = {State}, ");
        builder.Append($"CompanyName = {CompanyName}, RequestedAmount = {RequestedAmount}, Ssn = ***");
        return true;
    }
}
