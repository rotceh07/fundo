using System.Collections.Concurrent;
using Fundo.ExternalApi;

// Mock of the external system that receives approved customers from the Fundo backend.
// State lives in memory and is lost on restart. Request bodies are never logged.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ConcurrentDictionary<Guid, ExternalCustomerRequest>>();

var app = builder.Build();

var customers = app.MapGroup("/api/customers");

// Idempotent by CustomerId: repeating the same POST returns 200 without creating a duplicate,
// which lets the backend retry safely. A different body for an existing customer is a conflict.
customers.MapPost("", (ExternalCustomerRequest request, ConcurrentDictionary<Guid, ExternalCustomerRequest> store) =>
{
    if (store.TryAdd(request.CustomerId, request))
    {
        return Results.Ok();
    }

    return store[request.CustomerId] == request ? Results.Ok() : Results.Conflict();
});

// Replaces the stored customer, so repeating the same PUT is idempotent.
customers.MapPut("{customerId:guid}", (Guid customerId, ExternalCustomerRequest request, ConcurrentDictionary<Guid, ExternalCustomerRequest> store) =>
{
    if (customerId != request.CustomerId)
    {
        return Results.BadRequest();
    }

    if (!store.ContainsKey(customerId))
    {
        return Results.NotFound();
    }

    store[customerId] = request;
    return Results.Ok();
});

// Diagnostic view for demos. It never returns the SSN or the address.
customers.MapGet("", (ConcurrentDictionary<Guid, ExternalCustomerRequest> store) =>
    store.Values.Select(customer => new ExternalCustomerSummary(
        customer.CustomerId,
        customer.ApplicationId,
        customer.FirstName,
        customer.LastName,
        customer.State,
        customer.CompanyName,
        customer.RequestedAmount)));

app.Run();
