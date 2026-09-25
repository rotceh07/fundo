using System.Net;
using System.Text.Json;
using Fundo.Application.Applications.Events;
using Fundo.Infrastructure.ExternalServices;

namespace Fundo.Infrastructure.Tests.ExternalServices;

public class ExternalApplicationClientTests
{
    private readonly CapturingHandler _handler = new();
    private readonly ExternalApplicationClient _client;

    public ExternalApplicationClientTests()
    {
        _client = new ExternalApplicationClient(
            new HttpClient(_handler) { BaseAddress = new Uri("http://localhost:5180") });
    }

    private static ApplicationApprovedEvent NewEvent(ApplicationEventOperation operation) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Jane",
            "Smith",
            "200 Oak Ave",
            "TX",
            "Globex",
            "123456789",
            25_000.50m,
            operation);

    [Fact]
    public async Task SendAsync_WithCreateEvent_PostsSnapshotToCustomers()
    {
        var applicationEvent = NewEvent(ApplicationEventOperation.Create);

        await _client.SendAsync(applicationEvent, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, _handler.Method);
        Assert.Equal("/api/customers", _handler.Path);

        using var body = JsonDocument.Parse(_handler.Body!);
        var root = body.RootElement;
        Assert.Equal(applicationEvent.CustomerId, root.GetProperty("customerId").GetGuid());
        Assert.Equal(applicationEvent.ApplicationId, root.GetProperty("applicationId").GetGuid());
        Assert.Equal("Jane", root.GetProperty("firstName").GetString());
        Assert.Equal("Smith", root.GetProperty("lastName").GetString());
        Assert.Equal("200 Oak Ave", root.GetProperty("address").GetString());
        Assert.Equal("TX", root.GetProperty("state").GetString());
        Assert.Equal("Globex", root.GetProperty("companyName").GetString());
        Assert.Equal("123456789", root.GetProperty("ssn").GetString());
        Assert.Equal(25_000.50m, root.GetProperty("requestedAmount").GetDecimal());
        Assert.False(root.TryGetProperty("operation", out _));
    }

    [Fact]
    public async Task SendAsync_WithUpdateEvent_PutsSnapshotToCustomerRoute()
    {
        var applicationEvent = NewEvent(ApplicationEventOperation.Update);

        await _client.SendAsync(applicationEvent, CancellationToken.None);

        Assert.Equal(HttpMethod.Put, _handler.Method);
        Assert.Equal($"/api/customers/{applicationEvent.CustomerId}", _handler.Path);

        using var body = JsonDocument.Parse(_handler.Body!);
        Assert.Equal(applicationEvent.CustomerId, body.RootElement.GetProperty("customerId").GetGuid());
        Assert.Equal(25_000.50m, body.RootElement.GetProperty("requestedAmount").GetDecimal());
    }

    [Fact]
    public async Task SendAsync_WithNonSuccessStatus_ThrowsHttpRequestException()
    {
        _handler.StatusCode = HttpStatusCode.InternalServerError;

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _client.SendAsync(NewEvent(ApplicationEventOperation.Create), CancellationToken.None));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
    }

    [Fact]
    public async Task SendAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _client.SendAsync(NewEvent(ApplicationEventOperation.Create), cancellation.Token));
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        public HttpMethod? Method { get; private set; }

        public string? Path { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Method = request.Method;
            Path = request.RequestUri!.AbsolutePath;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(StatusCode);
        }
    }
}
