namespace Fundo.Api.Contracts;

public sealed record SubmitApplicationResponse(
    bool IsApproved,
    Guid? ApplicationId,
    string? DenialReason);
