using System.Text.Json;
using System.Text.Json.Serialization;
using Fundo.Application.Applications.Events;
using Fundo.Application.Applications.Submit;
using Fundo.Application.Rules;
using Fundo.Domain.Applications;
using Fundo.Domain.Customers;
using Fundo.Infrastructure.Outbox;
using Fundo.Infrastructure.Persistence;
using Fundo.Infrastructure.Persistence.Repositories;
using Fundo.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fundo.Infrastructure.Tests.Outbox;

// Runs the real handler, repositories, outbox publisher and unit of work over one SQLite database.
public class TransactionalOutboxTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private SqliteTestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await SqliteTestDatabase.CreateAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static SubmitApplicationCommand Submission(
        string ssn = "123-45-6789",
        string state = "FL",
        string firstName = "John",
        decimal amount = 10_000m) =>
        new(firstName, "Doe", "100 Main St", state, "Acme", amount, ssn);

    // Every collaborator shares the same DbContext, as they will inside one request scope.
    private static SubmitApplicationHandler CreateHandler(FundoDbContext context) =>
        new(
            new RuleEngine([new NewYorkRule(), new BlacklistedSsnRule(new EmptySsnBlacklist())]),
            new CustomerRepository(context),
            new LoanApplicationRepository(context),
            new OutboxApplicationEventPublisher(context),
            new UnitOfWork(context));

    private async Task<SubmitApplicationResult> SubmitAsync(SubmitApplicationCommand command)
    {
        await using var context = _database.CreateContext();
        return await CreateHandler(context).HandleAsync(command);
    }

    private static ApplicationApprovedEvent ReadPayload(OutboxMessage message) =>
        JsonSerializer.Deserialize<ApplicationApprovedEvent>(message.Payload!, ReadOptions)!;

    [Fact]
    public async Task HandleAsync_WithNewCustomer_PersistsCustomerApplicationAndOutboxMessage()
    {
        var result = await SubmitAsync(Submission());

        await using var context = _database.CreateContext();
        var customer = await context.Customers.SingleAsync();
        var application = await context.LoanApplications.SingleAsync();
        var message = await context.OutboxMessages.SingleAsync();
        var payload = ReadPayload(message);

        Assert.Equal(customer.Id, application.CustomerId);
        Assert.Equal(application.Id, result.ApplicationId);
        Assert.Equal(application.Id, payload.ApplicationId);
        Assert.Equal(customer.Id, payload.CustomerId);
        Assert.Equal(customer.Id, message.CustomerId);
        Assert.Equal("Create", message.Operation);
        Assert.Equal(ApplicationEventOperation.Create, payload.Operation);
    }

    [Fact]
    public async Task HandleAsync_WithReturningCustomer_UpdatesSameRecordsAndAppendsUpdateMessage()
    {
        await SubmitAsync(Submission());
        await SubmitAsync(Submission(ssn: "123456789", firstName: "Jane", amount: 25_000.50m));

        await using var context = _database.CreateContext();
        var customer = await context.Customers.SingleAsync();
        var application = await context.LoanApplications.SingleAsync();
        var messages = await context.OutboxMessages.OrderBy(x => x.Id).ToListAsync();

        Assert.Equal("Jane", customer.FirstName);
        Assert.Equal(25_000.50m, application.RequestedAmount);

        Assert.Equal(2, messages.Count);
        Assert.Equal("Create", messages[0].Operation);
        Assert.Equal("Update", messages[1].Operation);
        Assert.True(messages[0].Id < messages[1].Id);

        var create = ReadPayload(messages[0]);
        var update = ReadPayload(messages[1]);
        Assert.Equal(customer.Id, create.CustomerId);
        Assert.Equal(customer.Id, update.CustomerId);
        Assert.Equal(application.Id, create.ApplicationId);
        Assert.Equal(application.Id, update.ApplicationId);
        Assert.Equal("Jane", update.FirstName);
        Assert.Equal(25_000.50m, update.RequestedAmount);
    }

    [Fact]
    public async Task HandleAsync_WithSuccessiveSubmissions_KeepsOutboxInSubmissionOrder()
    {
        await SubmitAsync(Submission(amount: 10_000m));
        await SubmitAsync(Submission(amount: 20_000m));
        await SubmitAsync(Submission(amount: 30_000m));

        await using var context = _database.CreateContext();
        var messages = await context.OutboxMessages.OrderBy(x => x.Id).ToListAsync();

        Assert.Equal(["Create", "Update", "Update"], messages.Select(x => x.Operation));
        Assert.Equal([10_000m, 20_000m, 30_000m], messages.Select(x => ReadPayload(x).RequestedAmount));
        Assert.True(messages[0].Id < messages[1].Id && messages[1].Id < messages[2].Id);
    }

    [Fact]
    public async Task HandleAsync_WithDeniedSubmission_PersistsNothing()
    {
        var result = await SubmitAsync(Submission(state: "NY"));

        await using var context = _database.CreateContext();
        Assert.False(result.IsApproved);
        Assert.Equal(0, await context.Customers.CountAsync());
        Assert.Equal(0, await context.LoanApplications.CountAsync());
        Assert.Equal(0, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task StagedSubmission_BeforeSaveChanges_TracksAllThreeRecordsInSameContext()
    {
        var customer = Customer.Create("John", "Doe", "100 Main St", "FL", "Acme", Ssn.Create("123-45-6789"));
        var application = LoanApplication.Create(customer.Id, 10_000m);

        await using var context = _database.CreateContext();
        new CustomerRepository(context).Add(customer);
        new LoanApplicationRepository(context).Add(application);
        new OutboxApplicationEventPublisher(context).Publish(new ApplicationApprovedEvent(
            customer.Id,
            application.Id,
            customer.FirstName,
            customer.LastName,
            customer.Address,
            customer.State,
            customer.CompanyName,
            customer.Ssn.Value,
            application.RequestedAmount,
            ApplicationEventOperation.Create));

        Assert.Equal(EntityState.Added, context.Entry(customer).State);
        Assert.Equal(EntityState.Added, context.Entry(application).State);
        Assert.Equal(EntityState.Added, Assert.Single(context.ChangeTracker.Entries<OutboxMessage>()).State);
    }

    [Fact]
    public async Task HandleAsync_WhenSaveChangesFails_PersistsNoneOfTheNewRecords()
    {
        var existing = Customer.Create("Ann", "Lee", "1 Elm St", "FL", "Initech", Ssn.Create("111-11-1111"));
        var existingApplication = LoanApplication.Create(existing.Id, 5_000m);
        await using (var seed = _database.CreateContext())
        {
            seed.AddRange(existing, existingApplication);
            await seed.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            // A second application for the existing customer violates the unique CustomerId
            // index and makes the handler's single SaveChanges fail.
            context.LoanApplications.Add(LoanApplication.Create(existing.Id, 1_000m));

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                CreateHandler(context).HandleAsync(Submission(ssn: "222-22-2222")));
        }

        await using var verification = _database.CreateContext();
        Assert.Equal(existing.Id, (await verification.Customers.SingleAsync()).Id);
        Assert.Equal(existingApplication.Id, (await verification.LoanApplications.SingleAsync()).Id);
        Assert.Equal(0, await verification.OutboxMessages.CountAsync());
    }

    private sealed class EmptySsnBlacklist : ISsnBlacklist
    {
        public bool Contains(Ssn ssn) => false;
    }
}
