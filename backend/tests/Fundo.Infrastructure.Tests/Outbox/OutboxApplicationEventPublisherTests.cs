using System.Text.Json;
using System.Text.Json.Serialization;
using Fundo.Application.Applications.Events;
using Fundo.Infrastructure.Outbox;
using Fundo.Infrastructure.Persistence;
using Fundo.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fundo.Infrastructure.Tests.Outbox;

public class OutboxApplicationEventPublisherTests : IAsyncLifetime
{
    // The tests read the payload with their own options so they verify the stored contract,
    // not the publisher's internal configuration.
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private SqliteTestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await SqliteTestDatabase.CreateAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static ApplicationApprovedEvent NewEvent() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "John",
            "Doe",
            "100 Main St",
            "FL",
            "Acme",
            "123456789",
            12345.67m,
            ApplicationEventOperation.Create);

    private async Task<OutboxMessage> PublishAndSaveAsync(ApplicationApprovedEvent applicationEvent)
    {
        await using (var context = _database.CreateContext())
        {
            new OutboxApplicationEventPublisher(context).Publish(applicationEvent);
            await new UnitOfWork(context).SaveChangesAsync(CancellationToken.None);
        }

        await using var verification = _database.CreateContext();
        return await verification.OutboxMessages.SingleAsync();
    }

    [Fact]
    public async Task Publish_StagesOneAddedOutboxMessage()
    {
        await using var context = _database.CreateContext();

        new OutboxApplicationEventPublisher(context).Publish(NewEvent());

        var entry = Assert.Single(context.ChangeTracker.Entries<OutboxMessage>());
        Assert.Equal(EntityState.Added, entry.State);
    }

    [Fact]
    public async Task Publish_WithoutSaveChanges_DoesNotPersistMessage()
    {
        await using (var context = _database.CreateContext())
        {
            new OutboxApplicationEventPublisher(context).Publish(NewEvent());
        }

        await using var verification = _database.CreateContext();
        Assert.Equal(0, await verification.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Publish_ThenSaveChanges_PersistsMessageWithInitialMetadata()
    {
        var applicationEvent = NewEvent();
        var before = DateTimeOffset.UtcNow;

        var message = await PublishAndSaveAsync(applicationEvent);

        var after = DateTimeOffset.UtcNow;
        Assert.True(message.Id > 0);
        Assert.Equal(applicationEvent.CustomerId, message.CustomerId);
        Assert.Equal("ApplicationApprovedEvent", message.EventType);
        Assert.Equal("Create", message.Operation);
        Assert.InRange(message.CreatedAtUtc, before, after);
        Assert.Null(message.ProcessedAtUtc);
        Assert.Equal(0, message.RetryCount);
        Assert.Null(message.LastError);
        Assert.False(string.IsNullOrWhiteSpace(message.Payload));
    }

    [Fact]
    public async Task Publish_ThenSaveChanges_StoresPayloadThatDeserializesToSameEvent()
    {
        var applicationEvent = NewEvent();

        var message = await PublishAndSaveAsync(applicationEvent);

        var restored = JsonSerializer.Deserialize<ApplicationApprovedEvent>(message.Payload!, ReadOptions);
        Assert.Equal(applicationEvent, restored);
    }

    [Fact]
    public async Task Publish_ThenSaveChanges_StoresCanonicalSsnAndOperationAsText()
    {
        var message = await PublishAndSaveAsync(NewEvent());

        using var payload = JsonDocument.Parse(message.Payload!);
        Assert.Equal("123456789", payload.RootElement.GetProperty("Ssn").GetString());
        Assert.Equal("Create", payload.RootElement.GetProperty("Operation").GetString());
        Assert.DoesNotContain("***", message.Payload);
    }

    [Fact]
    public async Task Publish_WithNullEvent_ThrowsArgumentNullException()
    {
        await using var context = _database.CreateContext();
        var publisher = new OutboxApplicationEventPublisher(context);

        Assert.Throws<ArgumentNullException>(() => publisher.Publish(null!));
    }

    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new OutboxApplicationEventPublisher(null!));
    }
}
