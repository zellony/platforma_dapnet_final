using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Platform.Api.Infrastructure;
using Xunit;

namespace Platform.Tests.Api;

public class ReadOnlyMiddlewareTests
{
    [Fact]
    public async Task ReadOnlyUser_ModifyingEndpointOutsideWhitelist_ShouldReturn403_AndNotCallNext()
    {
        var nextCalled = false;
        var middleware = new ReadOnlyMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<ReadOnlyMiddleware>.Instance);

        var context = BuildHttpContext(
            method: "POST",
            path: "/admin/users",
            isReadOnly: true);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ReadOnlyUser_WhitelistedEndpoint_ShouldCallNext()
    {
        var nextCalled = false;
        var middleware = new ReadOnlyMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<ReadOnlyMiddleware>.Instance);

        var context = BuildHttpContext(
            method: "POST",
            path: "/auth/login",
            isReadOnly: true);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task NonReadOnlyUser_ModifyingEndpoint_ShouldCallNext()
    {
        var nextCalled = false;
        var middleware = new ReadOnlyMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<ReadOnlyMiddleware>.Instance);

        var context = BuildHttpContext(
            method: "POST",
            path: "/admin/users",
            isReadOnly: false);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ReadOnlyUser_NonModifyingEndpoint_ShouldCallNext()
    {
        var nextCalled = false;
        var middleware = new ReadOnlyMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<ReadOnlyMiddleware>.Instance);

        var context = BuildHttpContext(
            method: "GET",
            path: "/admin/users",
            isReadOnly: true);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    private static DefaultHttpContext BuildHttpContext(string method, string path, bool isReadOnly)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        var claims = new List<Claim>();
        if (isReadOnly)
            claims.Add(new Claim("is_read_only", "true"));

        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        return context;
    }
}

