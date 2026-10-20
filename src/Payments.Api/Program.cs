using Payments.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();

// Exposes the entry point to WebApplicationFactory in the integration tests.
public partial class Program;
