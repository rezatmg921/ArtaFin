using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArtaFin.UnitTests.Api;

public sealed class AuthenticationRateLimitTests
{
    [Fact]
    public async Task Register_WhenMoreThanTenRequestsArriveWithinOneMinute_ShouldReturnTooManyRequests()
    {
        using var factory = new ArtaFinApiFactory();
        using var client = factory.CreateClient();

        for (var requestNumber = 1; requestNumber <= 10; requestNumber++)
        {
            using var response = await client.PostAsJsonAsync(
                "/api/auth/register",
                new { email = "not-an-email", password = "weak" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var rejectedResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "not-an-email", password = "weak" });

        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
    }

    private sealed class ArtaFinApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(
            Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:Jwt:Issuer"] = "ArtaFin.IntegrationTests",
                    ["Authentication:Jwt:Audience"] = "ArtaFin.IntegrationTests.Client",
                    ["Authentication:Jwt:SigningKey"] = "integration-test-signing-key-at-least-32-bytes",
                    ["Authentication:Jwt:AccessTokenMinutes"] = "15",
                    ["ConnectionStrings:ArtaFin"] =
                        "Server=localhost;Database=ArtaFin_RateLimitTests;User ID=sa;Password=NotUsed_OnlyForHostStartup!;TrustServerCertificate=True"
                });
            });
        }
    }
}
