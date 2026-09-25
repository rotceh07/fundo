using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fundo.Api.Contracts;
using Fundo.Api.Tests.Infrastructure;
using Fundo.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace Fundo.Api.Tests;

// End to end through the real HTTP pipeline: routing, validation, DI, handler, rules, EF Core and SQLite.
public class ApplicationsEndpointTests
{
    private const string Endpoint = "/api/applications";

    private static JsonObject Request(
        string ssn = "123-45-6789",
        string state = "FL",
        decimal amount = 10_000m,
        string firstName = "Jane",
        string lastName = "Doe",
        string address = "100 Main St",
        string companyName = "Acme") =>
        new()
        {
            ["firstName"] = firstName,
            ["lastName"] = lastName,
            ["address"] = address,
            ["state"] = state,
            ["companyName"] = companyName,
            ["requestedAmount"] = amount,
            ["ssn"] = ssn
        };

    private static async Task<SubmitApplicationResponse> SubmitAsync(HttpClient client, JsonObject request)
    {
        var response = await client.PostAsJsonAsync(Endpoint, request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SubmitApplicationResponse>())!;
    }

    private static Task<(int Customers, int Applications, int Messages)> CountRowsAsync(FundoApiFactory factory) =>
        factory.QueryAsync(async db => (
            await db.Customers.CountAsync(),
            await db.LoanApplications.CountAsync(),
            await db.OutboxMessages.CountAsync()));

    [Fact]
    public async Task Post_WithNewApplicant_ApprovesAndPersistsCustomerApplicationAndOutbox()
    {
        await using var factory = new FundoApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(Endpoint, Request());
        var raw = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<SubmitApplicationResponse>(raw, JsonSerializerOptions.Web)!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(result.IsApproved);
        Assert.NotNull(result.ApplicationId);
        Assert.Null(result.DenialReason);

        Assert.DoesNotContain("ssn", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("123456789", raw);
        Assert.DoesNotContain("123-45-6789", raw);

        Assert.Equal((1, 1, 1), await CountRowsAsync(factory));
        var application = await factory.QueryAsync(db => db.LoanApplications.SingleAsync());
        Assert.Equal(result.ApplicationId, application.Id);
    }

    [Fact]
    public async Task Post_WithNewYorkState_DeniesWithoutPersisting()
    {
        await using var factory = new FundoApiFactory();

        var result = await SubmitAsync(factory.CreateClient(), Request(state: "ny"));

        Assert.False(result.IsApproved);
        Assert.Null(result.ApplicationId);
        Assert.Equal("Applications from NY are not eligible.", result.DenialReason);
        Assert.Equal((0, 0, 0), await CountRowsAsync(factory));
    }

    [Fact]
    public async Task Post_WithBlacklistedSsn_DeniesWithoutPersisting()
    {
        await using var factory = new FundoApiFactory();

        var result = await SubmitAsync(factory.CreateClient(), Request(ssn: "999-99-9999"));

        Assert.False(result.IsApproved);
        Assert.Null(result.ApplicationId);
        Assert.Equal("SSN is not eligible.", result.DenialReason);
        Assert.Equal((0, 0, 0), await CountRowsAsync(factory));
    }

    [Fact]
    public async Task Post_WithNewYorkStateAndBlacklistedSsn_ReportsNewYorkReason()
    {
        await using var factory = new FundoApiFactory();

        var result = await SubmitAsync(factory.CreateClient(), Request(state: "NY", ssn: "999999999"));

        Assert.Equal("Applications from NY are not eligible.", result.DenialReason);
    }

    [Fact]
    public async Task Post_WithReturningApplicant_UpdatesSameRecordsAndAppendsUpdateMessage()
    {
        await using var factory = new FundoApiFactory();
        var client = factory.CreateClient();

        var first = await SubmitAsync(client, Request());
        var customerId = await factory.QueryAsync(db => db.Customers.Select(x => x.Id).SingleAsync());

        var second = await SubmitAsync(client, Request(
            ssn: "123456789",
            state: "TX",
            amount: 25_000.75m,
            lastName: "Smith",
            address: "200 Oak Ave",
            companyName: "Globex"));

        Assert.True(first.IsApproved);
        Assert.True(second.IsApproved);
        Assert.Equal(first.ApplicationId, second.ApplicationId);
        Assert.Equal((1, 1, 2), await CountRowsAsync(factory));

        var customer = await factory.QueryAsync(db => db.Customers.SingleAsync());
        Assert.Equal(customerId, customer.Id);
        Assert.Equal(Ssn.Create("123456789"), customer.Ssn);
        Assert.Equal("Smith", customer.LastName);
        Assert.Equal("200 Oak Ave", customer.Address);
        Assert.Equal("TX", customer.State);
        Assert.Equal("Globex", customer.CompanyName);

        var application = await factory.QueryAsync(db => db.LoanApplications.SingleAsync());
        Assert.Equal(first.ApplicationId, application.Id);
        Assert.Equal(25_000.75m, application.RequestedAmount);

        var operations = await factory.QueryAsync(db =>
            db.OutboxMessages.OrderBy(x => x.Id).Select(x => x.Operation).ToListAsync());
        Assert.Equal(["Create", "Update"], operations);
    }

    [Theory]
    [InlineData("firstName", null, "FirstName")]
    [InlineData("firstName", "\"\"", "FirstName")]
    [InlineData("ssn", "\"12345\"", "Ssn")]
    [InlineData("ssn", "\"123\\t45\\t6789\"", "Ssn")]
    [InlineData("state", "\"New York\"", "State")]
    [InlineData("requestedAmount", "0", "RequestedAmount")]
    [InlineData("requestedAmount", "-1", "RequestedAmount")]
    public async Task Post_WithInvalidInput_Returns400WithoutPersisting(string field, string? jsonValue, string expectedErrorKey)
    {
        await using var factory = new FundoApiFactory();
        var request = Request();
        if (jsonValue is null)
        {
            request.Remove(field);
        }
        else
        {
            request[field] = JsonNode.Parse(jsonValue);
        }

        var response = await factory.CreateClient().PostAsJsonAsync(Endpoint, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errorKeys = problem.RootElement.GetProperty("errors").EnumerateObject().Select(x => x.Name);
        Assert.Contains(expectedErrorKey, errorKeys, StringComparer.OrdinalIgnoreCase);
        Assert.Equal((0, 0, 0), await CountRowsAsync(factory));
    }

    [Fact]
    public async Task Preflight_FromFrontendOrigin_AllowsThatOrigin()
    {
        await using var factory = new FundoApiFactory();
        using var preflight = new HttpRequestMessage(HttpMethod.Options, Endpoint);
        preflight.Headers.Add("Origin", "http://localhost:3000");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await factory.CreateClient().SendAsync(preflight);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("http://localhost:3000", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task Startup_AppliesAllMigrations()
    {
        await using var factory = new FundoApiFactory();
        factory.CreateClient();

        var applied = await factory.QueryAsync(db => db.Database.GetAppliedMigrationsAsync());

        Assert.Contains(applied, migration => migration.EndsWith("_InitialCreate"));
        Assert.Contains(applied, migration => migration.EndsWith("_AddTransactionalOutbox"));
    }

    [Fact]
    public async Task OpenApi_InDevelopment_DescribesApplicationsEndpoint()
    {
        await using var factory = new FundoApiFactory();

        var document = await factory.CreateClient().GetStringAsync("/openapi/v1.json");

        Assert.Contains("/api/applications", document);
    }
}
