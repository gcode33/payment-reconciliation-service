var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Placeholder until PLAN step 1.4 adds the card-company endpoints and chaos dials.
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
