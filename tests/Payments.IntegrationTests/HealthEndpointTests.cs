using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Payments.IntegrationTests;

public sealed class HealthEndpointTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Health_returns_ok_when_the_database_is_reachable()
    {
        await using var factory = CreateFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_returns_service_unavailable_when_the_database_is_unreachable()
    {
        // Nothing listens on port 1, so the connection is refused.
        await using var factory = CreateFactory("Host=127.0.0.1;Port=1;Username=x;Password=x;Timeout=2");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
            host.UseSetting("ConnectionStrings:Payments", connectionString));
}
