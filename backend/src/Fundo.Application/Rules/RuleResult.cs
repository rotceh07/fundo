namespace Fundo.Application.Rules;

public sealed record RuleResult
{
    private static readonly RuleResult Passed = new(isDenied: false, reason: null);

    private RuleResult(bool isDenied, string? reason)
    {
        IsDenied = isDenied;
        Reason = reason;
    }

    public bool IsDenied { get; }

    public string? Reason { get; }

    public static RuleResult Pass() => Passed;

    public static RuleResult Deny(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new RuleResult(isDenied: true, reason);
    }
}
