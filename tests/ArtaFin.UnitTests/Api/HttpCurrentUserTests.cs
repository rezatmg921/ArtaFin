using System.Security.Claims;
using ArtaFin.Api.CurrentUser;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ArtaFin.UnitTests.Api;

public sealed class HttpCurrentUserTests
{
    [Fact]
    public void UserId_ReturnsEmpty_WhenPrincipalIsUnauthenticated()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", Guid.NewGuid().ToString())]))
        };

        var currentUser = new HttpCurrentUser(new HttpContextAccessor
        {
            HttpContext = context
        });

        Assert.Equal(Guid.Empty, currentUser.UserId);
    }

    [Fact]
    public void UserId_ReturnsSubject_WhenPrincipalIsAuthenticated()
    {
        var expectedUserId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", expectedUserId.ToString())],
                authenticationType: "test"))
        };

        var currentUser = new HttpCurrentUser(new HttpContextAccessor
        {
            HttpContext = context
        });

        Assert.Equal(expectedUserId, currentUser.UserId);
    }

    [Fact]
    public void UserId_ReturnsEmpty_WhenAuthenticatedPrincipalHasInvalidSubject()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "not-a-guid")],
                authenticationType: "test"))
        };

        var currentUser = new HttpCurrentUser(new HttpContextAccessor
        {
            HttpContext = context
        });

        Assert.Equal(Guid.Empty, currentUser.UserId);
    }
}
