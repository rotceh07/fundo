using System.Text;

namespace Fundo.Application.Applications.Events;

public sealed record ApplicationApprovedEvent(
    Guid CustomerId,
    Guid ApplicationId,
    string FirstName,
    string LastName,
    string Address,
    string State,
    string CompanyName,
    string Ssn,
    decimal RequestedAmount,
    ApplicationEventOperation Operation)
{
    // Keeps the SSN out of the generated ToString as a safety net.
    // It does not replace the rule of never logging the whole event.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"CustomerId = {CustomerId}, ApplicationId = {ApplicationId}, ");
        builder.Append($"FirstName = {FirstName}, LastName = {LastName}, Address = {Address}, State = {State}, ");
        builder.Append($"CompanyName = {CompanyName}, Ssn = ***, RequestedAmount = {RequestedAmount}, Operation = {Operation}");
        return true;
    }
}
