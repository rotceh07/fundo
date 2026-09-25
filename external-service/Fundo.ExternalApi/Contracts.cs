namespace Fundo.ExternalApi;

public sealed record ExternalCustomerRequest(
    Guid CustomerId,
    Guid ApplicationId,
    string FirstName,
    string LastName,
    string Address,
    string State,
    string CompanyName,
    string Ssn,
    decimal RequestedAmount);

public sealed record ExternalCustomerSummary(
    Guid CustomerId,
    Guid ApplicationId,
    string FirstName,
    string LastName,
    string State,
    string CompanyName,
    decimal RequestedAmount);
