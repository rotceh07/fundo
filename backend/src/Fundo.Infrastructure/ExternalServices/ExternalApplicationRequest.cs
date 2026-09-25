namespace Fundo.Infrastructure.ExternalServices;

// HTTP contract sent to the external service. The operation is expressed by the HTTP method,
// so it is not part of the body.
internal sealed record ExternalApplicationRequest(
    Guid CustomerId,
    Guid ApplicationId,
    string FirstName,
    string LastName,
    string Address,
    string State,
    string CompanyName,
    string Ssn,
    decimal RequestedAmount);
