namespace Fundo.Application.Applications.Submit;

public sealed record SubmitApplicationResult
{
    private SubmitApplicationResult(bool isApproved, Guid? applicationId, string? denialReason)
    {
        IsApproved = isApproved;
        ApplicationId = applicationId;
        DenialReason = denialReason;
    }

    public bool IsApproved { get; }

    public Guid? ApplicationId { get; }

    public string? DenialReason { get; }

    public static SubmitApplicationResult Approved(Guid applicationId)
    {
        if (applicationId == Guid.Empty)
        {
            throw new ArgumentException("Application id is required.", nameof(applicationId));
        }

        return new SubmitApplicationResult(isApproved: true, applicationId, denialReason: null);
    }

    public static SubmitApplicationResult Denied(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new SubmitApplicationResult(isApproved: false, applicationId: null, reason);
    }
}
