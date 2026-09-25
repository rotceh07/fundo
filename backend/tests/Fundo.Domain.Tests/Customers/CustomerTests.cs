using Fundo.Domain.Customers;

namespace Fundo.Domain.Tests.Customers;

public class CustomerTests
{
    private static readonly Ssn DefaultSsn = Ssn.Create("123-45-6789");

    private static Customer CreateCustomer() =>
        Customer.Create("John", "Doe", "100 Main St", "FL", "Acme", DefaultSsn);

    [Fact]
    public void Create_WithValidData_CreatesCustomer()
    {
        var customer = CreateCustomer();

        Assert.NotEqual(Guid.Empty, customer.Id);
        Assert.Equal("John", customer.FirstName);
        Assert.Equal("Doe", customer.LastName);
        Assert.Equal("100 Main St", customer.Address);
        Assert.Equal("FL", customer.State);
        Assert.Equal("Acme", customer.CompanyName);
        Assert.Equal(DefaultSsn, customer.Ssn);
    }

    [Fact]
    public void Create_CalledTwice_GeneratesDifferentIds()
    {
        var first = CreateCustomer();
        var second = CreateCustomer();

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Create_WithSurroundingWhitespace_StoresTrimmedValues()
    {
        var customer = Customer.Create(" John ", " Doe ", " 100 Main St ", " fl ", " Acme ", DefaultSsn);

        Assert.Equal("John", customer.FirstName);
        Assert.Equal("Doe", customer.LastName);
        Assert.Equal("100 Main St", customer.Address);
        Assert.Equal("FL", customer.State);
        Assert.Equal("Acme", customer.CompanyName);
    }

    [Theory]
    [InlineData("ny")]
    [InlineData(" Ny ")]
    public void Create_WithMixedCaseState_StoresUppercaseState(string state)
    {
        var customer = Customer.Create("John", "Doe", "100 Main St", state, "Acme", DefaultSsn);

        Assert.Equal("NY", customer.State);
    }

    [Theory]
    [InlineData("", "Doe", "100 Main St", "FL", "Acme", "firstName")]
    [InlineData("John", " ", "100 Main St", "FL", "Acme", "lastName")]
    [InlineData("John", "Doe", "", "FL", "Acme", "address")]
    [InlineData("John", "Doe", "100 Main St", " ", "Acme", "state")]
    [InlineData("John", "Doe", "100 Main St", "FL", "", "companyName")]
    public void Create_WithMissingRequiredField_ThrowsArgumentException(
        string firstName,
        string lastName,
        string address,
        string state,
        string companyName,
        string expectedParameter)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Customer.Create(firstName, lastName, address, state, companyName, DefaultSsn));

        Assert.Equal(expectedParameter, exception.ParamName);
    }

    [Fact]
    public void Create_WithNullSsn_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Customer.Create("John", "Doe", "100 Main St", "FL", "Acme", null!));
    }

    [Fact]
    public void UpdateProfile_WithValidData_UpdatesMutableFields()
    {
        var customer = CreateCustomer();

        customer.UpdateProfile("Jane", "Smith", " 200 Oak Ave ", "tx", "Globex");

        Assert.Equal("Jane", customer.FirstName);
        Assert.Equal("Smith", customer.LastName);
        Assert.Equal("200 Oak Ave", customer.Address);
        Assert.Equal("TX", customer.State);
        Assert.Equal("Globex", customer.CompanyName);
    }

    [Fact]
    public void UpdateProfile_DoesNotChangeIdOrSsn()
    {
        var customer = CreateCustomer();
        var originalId = customer.Id;
        var originalSsn = customer.Ssn;

        customer.UpdateProfile("Jane", "Smith", "200 Oak Ave", "TX", "Globex");

        Assert.Equal(originalId, customer.Id);
        Assert.Equal(originalSsn, customer.Ssn);
    }

    [Fact]
    public void UpdateProfile_WithEmptyFirstName_ThrowsArgumentException()
    {
        var customer = CreateCustomer();

        Assert.Throws<ArgumentException>(() =>
            customer.UpdateProfile("", "Smith", "200 Oak Ave", "TX", "Globex"));
    }

    [Fact]
    public void UpdateProfile_WithInvalidData_KeepsPreviousValues()
    {
        var customer = CreateCustomer();

        Assert.Throws<ArgumentException>(() =>
            customer.UpdateProfile("Jane", "Smith", "200 Oak Ave", "TX", " "));

        Assert.Equal("John", customer.FirstName);
        Assert.Equal("Doe", customer.LastName);
        Assert.Equal("100 Main St", customer.Address);
        Assert.Equal("FL", customer.State);
        Assert.Equal("Acme", customer.CompanyName);
    }
}
