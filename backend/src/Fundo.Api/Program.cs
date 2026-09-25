using Fundo.Application.Abstractions;
using Fundo.Application.Applications.Submit;
using Fundo.Application.Rules;
using Fundo.Infrastructure;
using Fundo.Infrastructure.Outbox;
using Fundo.Infrastructure.Persistence;
using Fundo.Infrastructure.Persistence.Repositories;
using Fundo.Infrastructure.Rules;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

var connectionString = configuration.GetConnectionString("Fundo")
    ?? throw new InvalidOperationException("Connection string 'Fundo' is required.");

var externalServiceUrl = configuration["ExternalService:BaseUrl"]
    ?? throw new InvalidOperationException("External service base URL is required.");

if (!Uri.TryCreate(externalServiceUrl, UriKind.Absolute, out var externalServiceBaseAddress))
{
    throw new InvalidOperationException("External service base URL must be an absolute URI.");
}

var frontendOrigin = configuration["Frontend:Origin"]
    ?? throw new InvalidOperationException("Frontend origin is required.");

var blacklistedSsns = configuration
    .GetSection("Blacklist:Ssns")
    .GetChildren()
    .Select(section => section.Value
        ?? throw new InvalidOperationException("Blacklist SSN value is required."))
    .ToArray();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(frontendOrigin)
        .AllowAnyHeader()
        .AllowAnyMethod()));

// Persistence. Every scoped service below shares the request's DbContext, so the customer,
// the application and the outbox message are committed by the same SaveChanges.
builder.Services.AddDbContext<FundoDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ILoanApplicationRepository, LoanApplicationRepository>();
builder.Services.AddScoped<IApplicationEventPublisher, OutboxApplicationEventPublisher>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Rules. Registration order is evaluation order and the first denial wins,
// so the NY reason is reported when both rules match.
builder.Services.AddSingleton<ISsnBlacklist>(new InMemorySsnBlacklist(blacklistedSsns));
builder.Services.AddSingleton<IApplicationRule, NewYorkRule>();
builder.Services.AddSingleton<IApplicationRule, BlacklistedSsnRule>();
builder.Services.AddSingleton<RuleEngine>();

builder.Services.AddScoped<SubmitApplicationHandler>();

builder.Services.AddOutboxDelivery(externalServiceBaseAddress);

var app = builder.Build();

// Unexpected errors become a generic ProblemDetails 500 without stack traces or request data.
app.UseExceptionHandler();
app.UseCors("Frontend");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

// Applied at startup so the project runs with a single dotnet run. A production deployment
// would usually run migrations as a controlled deployment step instead.
await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<FundoDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Run();

public partial class Program
{
}
