using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Platform.Api.Core.Auth;
using Platform.Api.Core.Licensing;
using Platform.Api.Infrastructure;
using Platform.Api.Infrastructure.Database;
using Platform.Kernel.Core.RBAC.Entities;
using Xunit;

namespace Platform.Tests.Api;

public class AuthControllerSecurityTests
{
    [Fact]
    public async Task ServiceLogin_FromLoopback_WithValidPassword_ShouldSucceed()
    {
        var (controller, _) = BuildController(
            remoteIp: IPAddress.Loopback,
            serviceLogin: "AdminDAPNET",
            servicePassword: "svc-pass");

        var result = await controller.Login(new AuthController.LoginRequest("AdminDAPNET", "svc-pass", "PC-A", "127.0.0.1"));

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ServiceLogin_FromNonLoopback_ShouldBeUnauthorized()
    {
        var (controller, _) = BuildController(
            remoteIp: IPAddress.Parse("192.168.1.15"),
            serviceLogin: "AdminDAPNET",
            servicePassword: "svc-pass");

        var result = await controller.Login(new AuthController.LoginRequest("AdminDAPNET", "svc-pass", "PC-A", "192.168.1.15"));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task LogoutBeacon_WithValidToken_ShouldCloseOnlyMatchingSession()
    {
        var (controller, db) = BuildController(
            remoteIp: IPAddress.Loopback,
            serviceLogin: "AdminDAPNET",
            servicePassword: "svc-pass");

        var userId = Guid.NewGuid();
        var sessionToClose = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LoginAtUtc = DateTime.UtcNow,
            IpAddress = "127.0.0.1"
        };
        var sessionToKeep = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LoginAtUtc = DateTime.UtcNow,
            IpAddress = "127.0.0.1"
        };
        db.Users.Add(new User
        {
            Id = userId,
            Login = "user1",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("x"),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });
        db.UserSessions.AddRange(sessionToClose, sessionToKeep);
        await db.SaveChangesAsync();

        JwtKeyVault.CurrentKey = "integration_test_signing_key_32_chars_min_ok";
        var token = BuildJwt(
            key: JwtKeyVault.CurrentKey,
            issuer: "Platform.Api",
            audience: "Platform.Client",
            userId: userId,
            sessionId: sessionToClose.Id);

        var result = await controller.LogoutBeacon(token);

        result.Should().BeOfType<OkResult>();

        var updatedClose = await db.UserSessions.FirstAsync(x => x.Id == sessionToClose.Id);
        var updatedKeep = await db.UserSessions.FirstAsync(x => x.Id == sessionToKeep.Id);

        updatedClose.LogoutAtUtc.Should().NotBeNull();
        updatedClose.LogoutReason.Should().Be("Beacon");
        updatedKeep.LogoutAtUtc.Should().BeNull();
    }

    private static (AuthController Controller, AppDbContext Db) BuildController(
        IPAddress remoteIp,
        string serviceLogin,
        string servicePassword)
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(dbOptions);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceAccount:Enabled"] = "true",
                ["ServiceAccount:Login"] = serviceLogin,
                ["ServiceAccount:PasswordHash"] = BCrypt.Net.BCrypt.HashPassword(servicePassword),
                ["ServiceAccount:RateLimit:MaxAttempts"] = "5",
                ["ServiceAccount:RateLimit:LockoutMinutes"] = "10",
                ["Jwt:Issuer"] = "Platform.Api",
                ["Jwt:Audience"] = "Platform.Client"
            })
            .Build();

        var licenseService = new LicenseService(db, NullLogger<LicenseService>.Instance);
        var controller = new AuthController(
            db,
            config,
            licenseService,
            NullLogger<AuthController>.Instance,
            new RuntimeInstanceContext("test-instance"));

        var httpContext = new DefaultHttpContext
        {
            Connection = { RemoteIpAddress = remoteIp }
        };

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return (controller, db);
    }

    private static string BuildJwt(string key, string issuer, string audience, Guid userId, Guid sessionId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("session_id", sessionId.ToString())
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

