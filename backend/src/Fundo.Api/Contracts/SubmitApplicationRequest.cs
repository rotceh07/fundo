using System.ComponentModel.DataAnnotations;

namespace Fundo.Api.Contracts;

// Validates shape and format only. Eligibility (NY, blacklist) is decided by the rule engine,
// not rejected here.
public sealed class SubmitApplicationRequest : IValidatableObject
{
    [Required]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    public string LastName { get; init; } = string.Empty;

    [Required]
    public string Address { get; init; } = string.Empty;

    // A two letter code so a value like "New York" cannot bypass the NY rule.
    [Required]
    [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "State must be a two-letter code.")]
    public string State { get; init; } = string.Empty;

    [Required]
    public string CompanyName { get; init; } = string.Empty;

    public decimal RequestedAmount { get; init; }

    // Mirrors Ssn.Create: nine ASCII digits, optionally separated by spaces or dashes.
    // [0-9] instead of \d because \d also matches Unicode digits.
    [Required]
    [RegularExpression(@"^\s*(?:[0-9][ -]*){9}\s*$", ErrorMessage = "SSN must contain exactly 9 digits.")]
    public string Ssn { get; init; } = string.Empty;

    // Not a [Range] attribute because that works with double and would add a minimum the domain does not have.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RequestedAmount <= 0)
        {
            yield return new ValidationResult(
                "Requested amount must be greater than zero.",
                [nameof(RequestedAmount)]);
        }
    }
}
