using Fundo.Application.Abstractions;
using Fundo.Application.Applications.Events;
using Fundo.Application.Rules;
using Fundo.Domain.Applications;
using Fundo.Domain.Customers;

namespace Fundo.Application.Applications.Submit;

public sealed class SubmitApplicationHandler
{
    private readonly RuleEngine _ruleEngine;
    private readonly ICustomerRepository _customerRepository;
    private readonly ILoanApplicationRepository _applicationRepository;
    private readonly IApplicationEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitApplicationHandler(
        RuleEngine ruleEngine,
        ICustomerRepository customerRepository,
        ILoanApplicationRepository applicationRepository,
        IApplicationEventPublisher eventPublisher,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(ruleEngine);
        ArgumentNullException.ThrowIfNull(customerRepository);
        ArgumentNullException.ThrowIfNull(applicationRepository);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _ruleEngine = ruleEngine;
        _customerRepository = customerRepository;
        _applicationRepository = applicationRepository;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
    }

    public async Task<SubmitApplicationResult> HandleAsync(
        SubmitApplicationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ssn = Ssn.Create(command.Ssn);
        var ruleResult = _ruleEngine.Evaluate(ApplicationRuleContext.Create(command.State, ssn));

        if (ruleResult.IsDenied)
        {
            return SubmitApplicationResult.Denied(ruleResult.Reason!);
        }

        var customer = await _customerRepository.FindBySsnAsync(ssn, cancellationToken);
        LoanApplication application;
        ApplicationEventOperation operation;

        if (customer is null)
        {
            customer = Customer.Create(
                command.FirstName,
                command.LastName,
                command.Address,
                command.State,
                command.CompanyName,
                ssn);
            application = LoanApplication.Create(customer.Id, command.RequestedAmount);

            _customerRepository.Add(customer);
            _applicationRepository.Add(application);
            operation = ApplicationEventOperation.Create;
        }
        else
        {
            // Look up the application before touching the customer so an inconsistent
            // state fails without modifying anything.
            application = await _applicationRepository.FindByCustomerIdAsync(customer.Id, cancellationToken)
                ?? throw new InvalidOperationException("Existing customer has no loan application.");

            customer.UpdateProfile(
                command.FirstName,
                command.LastName,
                command.Address,
                command.State,
                command.CompanyName);
            application.UpdateRequestedAmount(command.RequestedAmount);

            _customerRepository.Update(customer);
            _applicationRepository.Update(application);
            operation = ApplicationEventOperation.Update;
        }

        // The event is staged before saving so the data changes and the event commit together.
        _eventPublisher.Publish(CreateEvent(customer, application, operation));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return SubmitApplicationResult.Approved(application.Id);
    }

    private static ApplicationApprovedEvent CreateEvent(
        Customer customer,
        LoanApplication application,
        ApplicationEventOperation operation) =>
        new(
            customer.Id,
            application.Id,
            customer.FirstName,
            customer.LastName,
            customer.Address,
            customer.State,
            customer.CompanyName,
            customer.Ssn.Value,
            application.RequestedAmount,
            operation);
}
