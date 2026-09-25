using System.Net;
using Fundo.Application.Applications.Events;
using Fundo.Infrastructure.ExternalServices;
using Fundo.Infrastructure.Outbox;
using Fundo.Infrastructure.Persistence;
using Fundo.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fundo.Infrastructure.Tests.Outbox;

public class OutboxProcessorTests : IAsyncLifetime
{
    private static readonly Guid CustomerA = Guid.NewGuid();
    private static readonly Guid CustomerB = Guid.NewGuid();

    private readonly FakeExternalClient _client = new();
    private SqliteTestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await SqliteTestDatabase.CreateAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static ApplicationApprovedEvent NewEvent(Guid customerId, ApplicationEventOperation operation) =>
        new(
            customerId,
            Guid.NewGuid(),
            "John",
            "Doe",
            "100 Main St",
            "FL",
            "Acme",
            "123456789",
            10_000m,
            operation);

    // Messages are saved one by one so their Ids follow the order given here.
    private async Task SeedAsync(params ApplicationApprovedEvent[] events)
    {
        foreach (var applicationEvent in events)
        {
            await using var context = _database.CreateContext();
            new OutboxApplicationEventPublisher(context).Publish(applicationEvent);
            await context.SaveChangesAsync();
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _database.CreateContext();
        await new OutboxProcessor(context, _client).ProcessPendingAsync(cancellationToken);
    }

    private async Task<List<OutboxMessage>> LoadMessagesAsync()
    {
        await using var context = _database.CreateContext();
        return await context.OutboxMessages.OrderBy(x => x.Id).ToListAsync();
    }

    private async Task CorruptAsync(Func<FundoDbContext, Task> corruption)
    {
        await using var context = _database.CreateContext();
        await corruption(context);
    }

    [Fact]
    public async Task ProcessPendingAsync_WithNoPendingMessages_DoesNotCallExternalService()
    {
        await ProcessAsync();

        Assert.Empty(_client.Calls);
    }

    [Fact]
    public async Task ProcessPendingAsync_WithPendingMessage_DeliversAndClearsPayload()
    {
        await SeedAsync(NewEvent(CustomerA, ApplicationEventOperation.Create));

        await ProcessAsync();

        Assert.Single(_client.Calls);
        var message = Assert.Single(await LoadMessagesAsync());
        Assert.NotNull(message.ProcessedAtUtc);
        Assert.Null(message.Payload);
        Assert.Null(message.LastError);
        Assert.Equal(0, message.RetryCount);
    }

    [Fact]
    public async Task ProcessPendingAsync_WithCreateThenUpdate_DeliversInOrder()
    {
        await SeedAsync(
            NewEvent(CustomerA, ApplicationEventOperation.Create),
            NewEvent(CustomerA, ApplicationEventOperation.Update));

        await ProcessAsync();

        Assert.Equal(
            [(CustomerA, ApplicationEventOperation.Create), (CustomerA, ApplicationEventOperation.Update)],
            _client.Calls);
        Assert.All(await LoadMessagesAsync(), message => Assert.NotNull(message.ProcessedAtUtc));
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenDeliveryFails_BlocksLaterMessagesOfSameCustomer()
    {
        await SeedAsync(
            NewEvent(CustomerA, ApplicationEventOperation.Create),
            NewEvent(CustomerA, ApplicationEventOperation.Update));
        _client.FailingCustomers.Add(CustomerA);

        await ProcessAsync();

        Assert.Equal([(CustomerA, ApplicationEventOperation.Create)], _client.Calls);

        var messages = await LoadMessagesAsync();
        Assert.Null(messages[0].ProcessedAtUtc);
        Assert.Equal(1, messages[0].RetryCount);
        Assert.NotNull(messages[0].Payload);
        Assert.Equal("External service returned HTTP 500.", messages[0].LastError);

        Assert.Null(messages[1].ProcessedAtUtc);
        Assert.Equal(0, messages[1].RetryCount);
        Assert.Null(messages[1].LastError);
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenOneCustomerFails_ContinuesWithOtherCustomers()
    {
        await SeedAsync(
            NewEvent(CustomerA, ApplicationEventOperation.Create),
            NewEvent(CustomerA, ApplicationEventOperation.Update),
            NewEvent(CustomerB, ApplicationEventOperation.Create));
        _client.FailingCustomers.Add(CustomerA);

        await ProcessAsync();

        Assert.Equal(
            [(CustomerA, ApplicationEventOperation.Create), (CustomerB, ApplicationEventOperation.Create)],
            _client.Calls);

        var messages = await LoadMessagesAsync();
        Assert.Equal(1, messages[0].RetryCount);
        Assert.Null(messages[0].ProcessedAtUtc);
        Assert.Equal(0, messages[1].RetryCount);
        Assert.Null(messages[1].ProcessedAtUtc);
        Assert.NotNull(messages[2].ProcessedAtUtc);
        Assert.Null(messages[2].Payload);
    }

    [Fact]
    public async Task ProcessPendingAsync_OnNextPass_RetriesFailedMessageBeforeLaterOnes()
    {
        await SeedAsync(
            NewEvent(CustomerA, ApplicationEventOperation.Create),
            NewEvent(CustomerA, ApplicationEventOperation.Update));
        _client.FailingCustomers.Add(CustomerA);
        await ProcessAsync();

        _client.FailingCustomers.Clear();
        _client.Calls.Clear();
        await ProcessAsync();

        Assert.Equal(
            [(CustomerA, ApplicationEventOperation.Create), (CustomerA, ApplicationEventOperation.Update)],
            _client.Calls);

        var messages = await LoadMessagesAsync();
        Assert.Equal(1, messages[0].RetryCount);
        Assert.All(messages, message =>
        {
            Assert.NotNull(message.ProcessedAtUtc);
            Assert.Null(message.Payload);
            Assert.Null(message.LastError);
        });
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "External service returned HTTP 500.")]
    [InlineData(null, "External service request failed.")]
    public async Task ProcessPendingAsync_WhenHttpFails_StoresGenericError(HttpStatusCode? statusCode, string expectedError)
    {
        await SeedAsync(NewEvent(CustomerA, ApplicationEventOperation.Create));
        _client.FailingCustomers.Add(CustomerA);
        _client.FailureStatus = statusCode;

        await ProcessAsync();

        var message = Assert.Single(await LoadMessagesAsync());
        Assert.Equal(expectedError, message.LastError);
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenRequestTimesOut_RegistersTimeoutFailure()
    {
        await SeedAsync(NewEvent(CustomerA, ApplicationEventOperation.Create));
        _client.ThrowTimeout = true;

        await ProcessAsync();

        var message = Assert.Single(await LoadMessagesAsync());
        Assert.Equal(1, message.RetryCount);
        Assert.Equal("External service request timed out.", message.LastError);
        Assert.NotNull(message.Payload);
        Assert.Null(message.ProcessedAtUtc);
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenHostIsStopping_PropagatesCancellationWithoutRegisteringFailure()
    {
        await SeedAsync(NewEvent(CustomerA, ApplicationEventOperation.Create));
        using var stopping = new CancellationTokenSource();
        _client.OnSend = () => stopping.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ProcessAsync(stopping.Token));

        var message = Assert.Single(await LoadMessagesAsync());
        Assert.Equal(0, message.RetryCount);
        Assert.Null(message.LastError);
        Assert.Null(message.ProcessedAtUtc);
    }

    [Fact]
    public async Task ProcessPendingAsync_WithMalformedPayload_RegistersFailureWithoutCallingService()
    {
        await SeedAsync(NewEvent(CustomerA, ApplicationEventOperation.Create));
        await CorruptAsync(context => context.OutboxMessages
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Payload, "{not json")));

        await ProcessAsync();

        Assert.Empty(_client.Calls);
        var message = Assert.Single(await LoadMessagesAsync());
        Assert.Equal(1, message.RetryCount);
        Assert.Equal("Invalid outbox payload.", message.LastError);
        Assert.Equal("{not json", message.Payload);
        Assert.Null(message.ProcessedAtUtc);
    }

    [Fact]
    public async Task ProcessPendingAsync_WithMissingPayload_RegistersFailureWithoutCallingService()
    {
        await SeedAsync(NewEvent(CustomerA, ApplicationEventOperation.Create));
        await CorruptAsync(context => context.OutboxMessages
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Payload, (string?)null)));

        await ProcessAsync();

        Assert.Empty(_client.Calls);
        var message = Assert.Single(await LoadMessagesAsync());
        Assert.Equal("Pending outbox message has no payload.", message.LastError);
        Assert.Null(message.ProcessedAtUtc);
    }

    [Fact]
    public async Task ProcessPendingAsync_WithCustomerMismatch_RegistersFailureWithoutCallingService()
    {
        await SeedAsync(
            NewEvent(CustomerA, ApplicationEventOperation.Create),
            NewEvent(CustomerA, ApplicationEventOperation.Update));
        var firstId = (await LoadMessagesAsync())[0].Id;
        await CorruptAsync(context => context.OutboxMessages
            .Where(x => x.Id == firstId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.CustomerId, CustomerB)));

        await ProcessAsync();

        Assert.Equal([(CustomerA, ApplicationEventOperation.Update)], _client.Calls);
        var mismatched = (await LoadMessagesAsync())[0];
        Assert.Equal(1, mismatched.RetryCount);
        Assert.Equal("Outbox payload customer does not match message metadata.", mismatched.LastError);
        Assert.Null(mismatched.ProcessedAtUtc);
    }

    private sealed class FakeExternalClient : IExternalApplicationClient
    {
        public List<(Guid CustomerId, ApplicationEventOperation Operation)> Calls { get; } = [];

        public HashSet<Guid> FailingCustomers { get; } = [];

        public HttpStatusCode? FailureStatus { get; set; } = HttpStatusCode.InternalServerError;

        public bool ThrowTimeout { get; set; }

        public Action? OnSend { get; set; }

        public Task SendAsync(ApplicationApprovedEvent applicationEvent, CancellationToken cancellationToken)
        {
            Calls.Add((applicationEvent.CustomerId, applicationEvent.Operation));
            OnSend?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();

            if (ThrowTimeout)
            {
                throw new TaskCanceledException("Simulated timeout.");
            }

            if (FailingCustomers.Contains(applicationEvent.CustomerId))
            {
                throw new HttpRequestException("Simulated failure.", null, FailureStatus);
            }

            return Task.CompletedTask;
        }
    }
}
