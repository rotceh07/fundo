using Fundo.Application.Abstractions;
using Fundo.Application.Applications.Events;
using Fundo.Application.Applications.Submit;
using Fundo.Application.Rules;
using Fundo.Application.Tests.Rules;
using Fundo.Domain.Applications;
using Fundo.Domain.Customers;

namespace Fundo.Application.Tests.Applications.Submit;

public class SubmitApplicationHandlerTests
{
    private const string AllowedSsn = "123-45-6789";
    private const string BlacklistedSsn = "111-11-1111";

    // Every fake writes to this log so tests can assert which calls happened and in what order.
    private readonly List<string> _calls = [];
    private readonly FakeCustomerRepository _customers;
    private readonly FakeLoanApplicationRepository _applications;
    private readonly FakeApplicationEventPublisher _publisher;
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly SubmitApplicationHandler _handler;

    public SubmitApplicationHandlerTests()
    {
        _customers = new FakeCustomerRepository(_calls);
        _applications = new FakeLoanApplicationRepository(_calls);
        _publisher = new FakeApplicationEventPublisher(_calls);
        _unitOfWork = new FakeUnitOfWork(_calls);

        var ruleEngine = new RuleEngine(
        [
            new NewYorkRule(),
            new BlacklistedSsnRule(new FakeSsnBlacklist(Ssn.Create(BlacklistedSsn)))
        ]);

        _handler = new SubmitApplicationHandler(ruleEngine, _customers, _applications, _publisher, _unitOfWork);
    }

    private static SubmitApplicationCommand NewCommand(string state = "fl", string ssn = AllowedSsn) =>
        new(" John ", "Doe", "100 Main St", state, "Acme", 10_000m, ssn);

    private (Customer Customer, LoanApplication Application) SeedExistingCustomer()
    {
        var customer = Customer.Create("John", "Doe", "100 Main St", "FL", "Acme", Ssn.Create(AllowedSsn));
        var application = LoanApplication.Create(customer.Id, 10_000m);

        _customers.ExistingCustomer = customer;
        _applications.ExistingApplication = application;

        return (customer, application);
    }

    [Fact]
    public async Task HandleAsync_WithNewYorkState_DeniesWithoutTouchingPersistence()
    {
        var result = await _handler.HandleAsync(NewCommand(state: "NY"));

        Assert.False(result.IsApproved);
        Assert.Equal("Applications from NY are not eligible.", result.DenialReason);
        Assert.Empty(_calls);
    }

    [Fact]
    public async Task HandleAsync_WithBlacklistedSsn_DeniesWithoutTouchingPersistence()
    {
        var result = await _handler.HandleAsync(NewCommand(ssn: BlacklistedSsn));

        Assert.False(result.IsApproved);
        Assert.Equal("SSN is not eligible.", result.DenialReason);
        Assert.Empty(_calls);
    }

    [Fact]
    public async Task HandleAsync_WithReturningCustomerNowInNewYork_DeniesAndLeavesExistingDataUntouched()
    {
        var (customer, application) = SeedExistingCustomer();
        var command = new SubmitApplicationCommand("Jane", "Smith", "1 Broadway", "NY", "Globex", 25_000m, AllowedSsn);

        var result = await _handler.HandleAsync(command);

        Assert.False(result.IsApproved);
        Assert.Empty(_calls);
        Assert.Equal("John", customer.FirstName);
        Assert.Equal("FL", customer.State);
        Assert.Equal(10_000m, application.RequestedAmount);
    }

    [Fact]
    public async Task HandleAsync_WithNewCustomer_CreatesCustomerAndApplicationAndSavesOnce()
    {
        var result = await _handler.HandleAsync(NewCommand());

        Assert.Equal(
            ["FindCustomerBySsn", "AddCustomer", "AddApplication", "Publish", "SaveChanges"],
            _calls);

        var customer = Assert.IsType<Customer>(_customers.AddedCustomer);
        Assert.Equal("John", customer.FirstName);
        Assert.Equal("Doe", customer.LastName);
        Assert.Equal("100 Main St", customer.Address);
        Assert.Equal("FL", customer.State);
        Assert.Equal("Acme", customer.CompanyName);
        Assert.Equal(Ssn.Create(AllowedSsn), customer.Ssn);

        var application = Assert.IsType<LoanApplication>(_applications.AddedApplication);
        Assert.NotEqual(Guid.Empty, application.Id);
        Assert.Equal(customer.Id, application.CustomerId);
        Assert.Equal(10_000m, application.RequestedAmount);

        Assert.True(result.IsApproved);
        Assert.Equal(application.Id, result.ApplicationId);
    }

    [Fact]
    public async Task HandleAsync_WithNewCustomer_PublishesCreateEventWithApprovedData()
    {
        await _handler.HandleAsync(NewCommand());

        var customer = _customers.AddedCustomer!;
        var application = _applications.AddedApplication!;
        var expected = new ApplicationApprovedEvent(
            customer.Id,
            application.Id,
            "John",
            "Doe",
            "100 Main St",
            "FL",
            "Acme",
            "123456789",
            10_000m,
            ApplicationEventOperation.Create);

        Assert.Equal(expected, _publisher.PublishedEvent);
    }

    [Fact]
    public async Task HandleAsync_WithReturningCustomer_UpdatesSameCustomerAndApplication()
    {
        var (customer, application) = SeedExistingCustomer();
        var originalCustomerId = customer.Id;
        var originalSsn = customer.Ssn;
        var originalApplicationId = application.Id;
        var command = new SubmitApplicationCommand("Jane", "Smith", "200 Oak Ave", "tx", "Globex", 25_000m, "123456789");

        var result = await _handler.HandleAsync(command);

        Assert.Equal(
            ["FindCustomerBySsn", "FindApplicationByCustomerId", "UpdateCustomer", "UpdateApplication", "Publish", "SaveChanges"],
            _calls);
        Assert.Same(customer, _customers.UpdatedCustomer);
        Assert.Same(application, _applications.UpdatedApplication);

        Assert.Equal(originalCustomerId, customer.Id);
        Assert.Equal(originalSsn, customer.Ssn);
        Assert.Equal(originalApplicationId, application.Id);
        Assert.Equal(originalCustomerId, application.CustomerId);

        Assert.Equal("Jane", customer.FirstName);
        Assert.Equal("Smith", customer.LastName);
        Assert.Equal("200 Oak Ave", customer.Address);
        Assert.Equal("TX", customer.State);
        Assert.Equal("Globex", customer.CompanyName);
        Assert.Equal(25_000m, application.RequestedAmount);

        Assert.True(result.IsApproved);
        Assert.Equal(originalApplicationId, result.ApplicationId);
    }

    [Fact]
    public async Task HandleAsync_WithReturningCustomer_PublishesUpdateEventWithLatestData()
    {
        var (customer, application) = SeedExistingCustomer();
        var command = new SubmitApplicationCommand("Jane", "Smith", "200 Oak Ave", "TX", "Globex", 25_000m, AllowedSsn);

        await _handler.HandleAsync(command);

        var expected = new ApplicationApprovedEvent(
            customer.Id,
            application.Id,
            "Jane",
            "Smith",
            "200 Oak Ave",
            "TX",
            "Globex",
            "123456789",
            25_000m,
            ApplicationEventOperation.Update);

        Assert.Equal(expected, _publisher.PublishedEvent);
    }

    [Fact]
    public async Task HandleAsync_WhenExistingCustomerHasNoApplication_ThrowsWithoutChangingAnything()
    {
        var (customer, _) = SeedExistingCustomer();
        _applications.ExistingApplication = null;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleAsync(new SubmitApplicationCommand("Jane", "Smith", "200 Oak Ave", "TX", "Globex", 25_000m, AllowedSsn)));

        Assert.Equal("Existing customer has no loan application.", exception.Message);
        Assert.Equal(["FindCustomerBySsn", "FindApplicationByCustomerId"], _calls);
        Assert.Equal("John", customer.FirstName);
    }

    [Fact]
    public async Task HandleAsync_WhenPublishFails_DoesNotSaveChanges()
    {
        _publisher.ExceptionToThrow = new InvalidOperationException("Publish failed.");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.HandleAsync(NewCommand()));

        Assert.Contains("Publish", _calls);
        Assert.DoesNotContain("SaveChanges", _calls);
    }

    [Fact]
    public async Task HandleAsync_WhenSaveChangesFails_PropagatesException()
    {
        _unitOfWork.ExceptionToThrow = new InvalidOperationException("Save failed.");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.HandleAsync(NewCommand()));

        Assert.Equal("Save failed.", exception.Message);
    }

    [Fact]
    public async Task HandleAsync_WithCancellationToken_PassesSameTokenToDependencies()
    {
        SeedExistingCustomer();
        using var cancellationSource = new CancellationTokenSource();
        var token = cancellationSource.Token;

        await _handler.HandleAsync(NewCommand(), token);

        Assert.Equal(token, _customers.ReceivedToken);
        Assert.Equal(token, _applications.ReceivedToken);
        Assert.Equal(token, _unitOfWork.ReceivedToken);
    }

    [Fact]
    public async Task HandleAsync_WithNullCommand_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.HandleAsync(null!));

        Assert.Empty(_calls);
    }

    private sealed class FakeCustomerRepository(List<string> calls) : ICustomerRepository
    {
        public Customer? ExistingCustomer { get; set; }

        public Customer? AddedCustomer { get; private set; }

        public Customer? UpdatedCustomer { get; private set; }

        public CancellationToken ReceivedToken { get; private set; }

        public Task<Customer?> FindBySsnAsync(Ssn ssn, CancellationToken cancellationToken)
        {
            calls.Add("FindCustomerBySsn");
            ReceivedToken = cancellationToken;
            return Task.FromResult(ExistingCustomer?.Ssn == ssn ? ExistingCustomer : null);
        }

        public void Add(Customer customer)
        {
            calls.Add("AddCustomer");
            AddedCustomer = customer;
        }

        public void Update(Customer customer)
        {
            calls.Add("UpdateCustomer");
            UpdatedCustomer = customer;
        }
    }

    private sealed class FakeLoanApplicationRepository(List<string> calls) : ILoanApplicationRepository
    {
        public LoanApplication? ExistingApplication { get; set; }

        public LoanApplication? AddedApplication { get; private set; }

        public LoanApplication? UpdatedApplication { get; private set; }

        public CancellationToken ReceivedToken { get; private set; }

        public Task<LoanApplication?> FindByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken)
        {
            calls.Add("FindApplicationByCustomerId");
            ReceivedToken = cancellationToken;
            return Task.FromResult(ExistingApplication?.CustomerId == customerId ? ExistingApplication : null);
        }

        public void Add(LoanApplication application)
        {
            calls.Add("AddApplication");
            AddedApplication = application;
        }

        public void Update(LoanApplication application)
        {
            calls.Add("UpdateApplication");
            UpdatedApplication = application;
        }
    }

    private sealed class FakeApplicationEventPublisher(List<string> calls) : IApplicationEventPublisher
    {
        public ApplicationApprovedEvent? PublishedEvent { get; private set; }

        public Exception? ExceptionToThrow { get; set; }

        public void Publish(ApplicationApprovedEvent applicationEvent)
        {
            calls.Add("Publish");

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            PublishedEvent = applicationEvent;
        }
    }

    private sealed class FakeUnitOfWork(List<string> calls) : IUnitOfWork
    {
        public Exception? ExceptionToThrow { get; set; }

        public CancellationToken ReceivedToken { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            calls.Add("SaveChanges");
            ReceivedToken = cancellationToken;

            return ExceptionToThrow is null ? Task.CompletedTask : Task.FromException(ExceptionToThrow);
        }
    }
}
