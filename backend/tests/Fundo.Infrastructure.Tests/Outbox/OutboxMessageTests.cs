using Fundo.Application.Applications.Events;
using Fundo.Infrastructure.Outbox;
using Fundo.Infrastructure.Tests.Persistence;

namespace Fundo.Infrastructure.Tests.Outbox;

public class OutboxMessageTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await SqliteTestDatabase.CreateAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    // Messages are created through the real publisher because the factory is internal.
    private OutboxMessage NewMessage()
    {
        using var context = _database.CreateContext();
        new OutboxApplicationEventPublisher(context).Publish(new ApplicationApprovedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "John",
            "Doe",
            "100 Main St",
            "FL",
            "Acme",
            "123456789",
            10_000m,
            ApplicationEventOperation.Create));

        return context.ChangeTracker.Entries<OutboxMessage>().Single().Entity;
    }

    [Fact]
    public void MarkProcessed_ClearsPayloadAndErrorAndKeepsRetryCount()
    {
        var message = NewMessage();
        message.RegisterFailure("External service request failed.");
        var processedAt = DateTimeOffset.UtcNow;

        message.MarkProcessed(processedAt);

        Assert.Equal(processedAt, message.ProcessedAtUtc);
        Assert.Null(message.Payload);
        Assert.Null(message.LastError);
        Assert.Equal(1, message.RetryCount);
    }

    [Fact]
    public void MarkProcessed_WithDefaultTime_ThrowsArgumentException()
    {
        var message = NewMessage();

        Assert.Throws<ArgumentException>(() => message.MarkProcessed(default));
    }

    [Fact]
    public void RegisterFailure_IncrementsRetryCountAndKeepsMessagePending()
    {
        var message = NewMessage();
        var payload = message.Payload;

        message.RegisterFailure("  External service returned HTTP 500.  ");

        Assert.Equal(1, message.RetryCount);
        Assert.Equal("External service returned HTTP 500.", message.LastError);
        Assert.Equal(payload, message.Payload);
        Assert.Null(message.ProcessedAtUtc);
    }

    [Fact]
    public void RegisterFailure_CalledTwice_CountsBothAndKeepsLatestError()
    {
        var message = NewMessage();

        message.RegisterFailure("External service returned HTTP 500.");
        message.RegisterFailure("External service request timed out.");

        Assert.Equal(2, message.RetryCount);
        Assert.Equal("External service request timed out.", message.LastError);
    }

    [Fact]
    public void RegisterFailure_WithLongError_TruncatesTo500Characters()
    {
        var message = NewMessage();

        message.RegisterFailure(new string('x', 800));

        Assert.Equal(500, message.LastError!.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterFailure_WithEmptyError_ThrowsArgumentException(string error)
    {
        var message = NewMessage();

        Assert.Throws<ArgumentException>(() => message.RegisterFailure(error));
    }
}
