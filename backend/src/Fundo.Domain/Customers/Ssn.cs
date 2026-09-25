namespace Fundo.Domain.Customers;

public sealed record Ssn
{
    private const int Length = 9;

    private Ssn(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Ssn Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);

        // The error messages never echo the input because an SSN is sensitive data.
        if (normalized.Length != Length)
        {
            throw new ArgumentException($"SSN must contain exactly {Length} digits.", nameof(value));
        }

        // IsAsciiDigit instead of IsDigit so Unicode digits from other scripts are rejected.
        if (!normalized.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("SSN must contain only digits.", nameof(value));
        }

        return new Ssn(normalized);
    }

    public override string ToString() => Value;
}
