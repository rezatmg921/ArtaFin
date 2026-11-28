using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
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

    [Fact]
    public async Task GetCurrentUser_ShouldReturnAllDistinctRoleClaims()
    {
        using var factory = new ArtaFinApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken());

        using var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal("a79a6250-9c5e-4e11-b793-55ec8340ed7d",
            root.GetProperty("userId").GetString());
        Assert.Equal("admin@artafin.com", root.GetProperty("email").GetString());
        Assert.Equal(
            new[] { "Administrator", "User" },
            root.GetProperty("roles").EnumerateArray()
                .Select(role => role.GetString())
                .ToArray());
    }

    private static string CreateAccessToken()
    {
        var now = DateTime.UtcNow;
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("integration-test-signing-key-at-least-32-bytes"));
        var token = new JwtSecurityToken(
            issuer: "ArtaFin.IntegrationTests",
            audience: "ArtaFin.IntegrationTests.Client",
            claims:
            [
                new Claim("sub", "a79a6250-9c5e-4e11-b793-55ec8340ed7d"),
                new Claim("email", "admin@artafin.com"),
                new Claim("role", "User"),
                new Claim("role", "Administrator"),
                new Claim("role", "User")
            ],
            notBefore: now,
            expires: now.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
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
