using Fundo.Domain.Applications;

namespace Fundo.Domain.Tests.Applications;

public class LoanApplicationTests
{
    public static TheoryData<decimal> NonPositiveAmounts => new() { 0m, -100m };

    [Fact]
    public void Create_WithValidData_CreatesApplication()
    {
        var customerId = Guid.NewGuid();

        var application = LoanApplication.Create(customerId, 10_000m);

        Assert.NotEqual(Guid.Empty, application.Id);
        Assert.Equal(customerId, application.CustomerId);
        Assert.Equal(10_000m, application.RequestedAmount);
    }

    [Fact]
    public void Create_WithEmptyCustomerId_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            LoanApplication.Create(Guid.Empty, 10_000m));

        Assert.Equal("customerId", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(NonPositiveAmounts))]
    public void Create_WithNonPositiveAmount_ThrowsArgumentOutOfRangeException(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LoanApplication.Create(Guid.NewGuid(), amount));
    }

    [Fact]
    public void UpdateRequestedAmount_WithValidAmount_UpdatesAmountOnly()
    {
        var application = LoanApplication.Create(Guid.NewGuid(), 10_000m);
        var originalId = application.Id;
        var originalCustomerId = application.CustomerId;

        application.UpdateRequestedAmount(25_000m);

        Assert.Equal(25_000m, application.RequestedAmount);
        Assert.Equal(originalId, application.Id);
        Assert.Equal(originalCustomerId, application.CustomerId);
    }

    [Theory]
    [MemberData(nameof(NonPositiveAmounts))]
    public void UpdateRequestedAmount_WithNonPositiveAmount_ThrowsAndKeepsPreviousAmount(decimal amount)
    {
        var application = LoanApplication.Create(Guid.NewGuid(), 10_000m);

        Assert.Throws<ArgumentOutOfRangeException>(() => application.UpdateRequestedAmount(amount));

        Assert.Equal(10_000m, application.RequestedAmount);
    }
}
