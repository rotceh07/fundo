using System.Runtime.CompilerServices;

namespace Fundo.Domain.Customers;

public sealed class Customer
{
    // Only used by EF Core when materializing from the database.
    private Customer()
    {
    }

    public Guid Id { get; private set; }

    public string FirstName { get; private set; } = null!;

    public string LastName { get; private set; } = null!;

    public string Address { get; private set; } = null!;

    public string State { get; private set; } = null!;

    public string CompanyName { get; private set; } = null!;

    public Ssn Ssn { get; private set; } = null!;

    public static Customer Create(
        string firstName,
        string lastName,
        string address,
        string state,
        string companyName,
        Ssn ssn)
    {
        ArgumentNullException.ThrowIfNull(ssn);

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Ssn = ssn
        };

        customer.UpdateProfile(firstName, lastName, address, state, companyName);

        return customer;
    }

    // Id and Ssn are intentionally left out: the SSN is the business identity
    // used to find a returning customer, so it never changes.
    public void UpdateProfile(
        string firstName,
        string lastName,
        string address,
        string state,
        string companyName)
    {
        // Validate everything before assigning so a failed update leaves the customer untouched.
        var validFirstName = Required(firstName);
        var validLastName = Required(lastName);
        var validAddress = Required(address);
        var validState = Required(state).ToUpperInvariant();
        var validCompanyName = Required(companyName);

        FirstName = validFirstName;
        LastName = validLastName;
        Address = validAddress;
        State = validState;
        CompanyName = validCompanyName;
    }

    private static string Required(
        string value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        return value.Trim();
    }
}
