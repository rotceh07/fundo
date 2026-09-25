namespace Fundo.Domain.Applications;

public sealed class LoanApplication
{
    // Only used by EF Core when materializing from the database.
    private LoanApplication()
    {
    }

    public Guid Id { get; private set; }

    public decimal RequestedAmount { get; private set; }

    public Guid CustomerId { get; private set; }

    public static LoanApplication Create(Guid customerId, decimal requestedAmount)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer id is required.", nameof(customerId));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedAmount);

        return new LoanApplication
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            RequestedAmount = requestedAmount
        };
    }

    public void UpdateRequestedAmount(decimal requestedAmount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedAmount);

        RequestedAmount = requestedAmount;
    }
}
