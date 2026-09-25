using System.Net.Http.Json;
using Fundo.Application.Applications.Events;

namespace Fundo.Infrastructure.ExternalServices;

public sealed class ExternalApplicationClient : IExternalApplicationClient
{
    private const string CustomersPath = "api/customers";

    private readonly HttpClient _httpClient;

    public ExternalApplicationClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
    }

    // Throws HttpRequestException for any non success status code.
    public async Task SendAsync(ApplicationApprovedEvent applicationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(applicationEvent);

        var request = new ExternalApplicationRequest(
            applicationEvent.CustomerId,
            applicationEvent.ApplicationId,
            applicationEvent.FirstName,
            applicationEvent.LastName,
            applicationEvent.Address,
            applicationEvent.State,
            applicationEvent.CompanyName,
            applicationEvent.Ssn,
            applicationEvent.RequestedAmount);

        using var response = applicationEvent.Operation switch
        {
            ApplicationEventOperation.Create =>
                await _httpClient.PostAsJsonAsync(CustomersPath, request, cancellationToken),
            ApplicationEventOperation.Update =>
                await _httpClient.PutAsJsonAsync($"{CustomersPath}/{applicationEvent.CustomerId}", request, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(applicationEvent), "Unsupported operation.")
        };

        response.EnsureSuccessStatusCode();
    }
}
