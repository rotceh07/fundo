using Fundo.Api.Contracts;
using Fundo.Application.Applications.Submit;
using Microsoft.AspNetCore.Mvc;

namespace Fundo.Api.Controllers;

[ApiController]
[Route("api/applications")]
public sealed class ApplicationsController : ControllerBase
{
    private readonly SubmitApplicationHandler _handler;

    public ApplicationsController(SubmitApplicationHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _handler = handler;
    }

    // Approved and denied are both valid decisions, so both return 200.
    // Invalid input is rejected with 400 by [ApiController] before reaching this action.
    [HttpPost]
    [ProducesResponseType<SubmitApplicationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SubmitApplicationResponse>> Submit(
        SubmitApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SubmitApplicationCommand(
            request.FirstName,
            request.LastName,
            request.Address,
            request.State,
            request.CompanyName,
            request.RequestedAmount,
            request.Ssn);

        var result = await _handler.HandleAsync(command, cancellationToken);

        return Ok(new SubmitApplicationResponse(result.IsApproved, result.ApplicationId, result.DenialReason));
    }
}
