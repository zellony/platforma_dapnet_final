using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using Platform.Api.Infrastructure.Database;
using Platform.Kernel.Core.RBAC.Entities;
using Platform.Api.Core.Licensing;
using Platform.Api.Infrastructure.Config;
using Platform.Api.Infrastructure;

namespace Platform.Api.Core.Auth;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, ServiceLoginAttemptState> ServiceLoginAttempts = new();

    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly LicenseService _licenseService;
    private readonly ILogger<AuthController> _logger;
    private readonly RuntimeInstanceContext _runtimeInstance;

    public AuthController(AppDbContext db, IConfiguration config, LicenseService licenseService, ILogger<AuthController> logger, RuntimeInstanceContext runtimeInstance)
    {
        _db = db;
        _config = config;
        _licenseService = licenseService;
        _logger = logger;
        _runtimeInstance = runtimeInstance;
    }

    public sealed record LoginRequest(string Login, string Password, string? MachineName, string? IpAddress, bool Force = false);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        string inputLogin = (req.Login ?? "").Trim();

        var serviceEnabled = _config.GetValue<bool>("ServiceAccount:Enabled", true);
        var serviceLogin = (_config["ServiceAccount:Login"] ?? "AdminDAPNET").Trim();
        var servicePasswordHash = (_config["ServiceAccount:PasswordHash"] ?? string.Empty).Trim();

        string password = req.Password;
        try
        {
            if (!string.IsNullOrEmpty(req.Password) && req.Password.Length > 100)
            {
                password = RsaKeyVault.Decrypt(req.Password);
            }
        }
        catch { }

        if (serviceEnabled && !string.IsNullOrWhiteSpace(serviceLogin) &&
            string.Equals(inputLogin, serviceLogin, StringComparison.OrdinalIgnoreCase))
        {
            var rateLimitKey = GetServiceRateLimitKey(serviceLogin);
            var maxAttempts = Math.Max(1, _config.GetValue<int?>("ServiceAccount:RateLimit:MaxAttempts") ?? 5);
            var lockoutMinutes = Math.Max(1, _config.GetValue<int?>("ServiceAccount:RateLimit:LockoutMinutes") ?? 10);

            if (TryGetServiceLockout(rateLimitKey, out var lockoutRemaining))
            {
                _logger.LogWarning("Service account locked. Key={Key}, RemainingSec={RemainingSec}", rateLimitKey, (int)Math.Ceiling(lockoutRemaining.TotalSeconds));
                return StatusCode(429, new { message = $"Zbyt wiele nieudanych prob. Sprobuj ponownie za {(int)Math.Ceiling(lockoutRemaining.TotalSeconds)} s." });
            }

            if (!IsRequestFromLocalMachine())
            {
                _logger.LogWarning("Service account login blocked from non-local address: {RemoteIp}", HttpContext.Connection.RemoteIpAddress?.ToString());
                return Unauthorized(new { message = "Niepoprawny login lub haslo" });
            }

            if (string.IsNullOrWhiteSpace(servicePasswordHash))
            {
                _logger.LogError("Service account is enabled but password hash is missing in configuration.");
                return StatusCode(500, new { message = "Konto serwisowe nie jest poprawnie skonfigurowane." });
            }

            if (BCrypt.Net.BCrypt.Verify(password, servicePasswordHash))
            {
                ResetServiceFailures(rateLimitKey);
                _logger.LogInformation(
                    "Service account login success. Login={Login}, Machine={Machine}, Ip={Ip}",
                    serviceLogin,
                    req.MachineName,
                    req.IpAddress ?? HttpContext.Connection.RemoteIpAddress?.ToString());

                return GenerateToken(
                    Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    serviceLogin,
                    true,
                    req.MachineName,
                    req.IpAddress,
                    saveSession: false,
                    isReadOnly: false);
            }

            _logger.LogWarning(
                "Service account login failed (invalid password). Login={Login}, Machine={Machine}, Ip={Ip}",
                serviceLogin,
                req.MachineName,
                req.IpAddress ?? HttpContext.Connection.RemoteIpAddress?.ToString());

            var lockoutTriggered = RegisterServiceFailure(rateLimitKey, maxAttempts, TimeSpan.FromMinutes(lockoutMinutes), out var retryAtUtc);
            if (lockoutTriggered)
            {
                _logger.LogWarning("Service account lockout activated. Key={Key}, RetryAtUtc={RetryAtUtc}", rateLimitKey, retryAtUtc?.UtcDateTime);
                return StatusCode(429, new { message = $"Zbyt wiele nieudanych prob. Sprobuj ponownie za {lockoutMinutes} min." });
            }

            return Unauthorized(new { message = "Niepoprawny login lub haslo" });
        }

        var user = await _db.Users.SingleOrDefaultAsync(u => u.Login == req.Login);
        if (user is null) return Unauthorized(new { message = "Niepoprawny login lub haslo" });
        if (!user.IsActive) return BadRequest(new { message = "Twoje konto zostalo zablokowane." });
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return Unauthorized(new { message = "Niepoprawny login lub haslo" });

        var license = await _licenseService.GetCurrentStatusAsync();
        bool isReadOnly = false;

        if (license.Message == "Brak licencji")
        {
            return BadRequest(new { message = "System wymaga aktywacji licencji przed zalogowaniem." });
        }

        if (!license.IsActive)
        {
            isReadOnly = true;
        }

        var activeSession = await _db.UserSessions.FirstOrDefaultAsync(s => s.UserId == user.Id && s.LogoutAtUtc == null);
        var autoRecoverSameMachine = _config.GetValue<bool?>("Auth:AutoRecoverSameMachineSession") ?? true;

        if (activeSession != null && !req.Force && autoRecoverSameMachine)
        {
            var incomingMachine = (req.MachineName ?? string.Empty).Trim();
            var existingMachine = (activeSession.MachineName ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(incomingMachine) &&
                !string.IsNullOrWhiteSpace(existingMachine) &&
                string.Equals(incomingMachine, existingMachine, StringComparison.OrdinalIgnoreCase))
            {
                activeSession.LogoutAtUtc = DateTime.UtcNow;
                activeSession.LogoutReason = "RecoveredAfterCrash";
                await _db.SaveChangesAsync();
                _logger.LogInformation("Recovered previous active session for user {UserId} on machine {Machine}.", user.Id, incomingMachine);
                activeSession = null;
            }
        }

        if (activeSession != null && !req.Force && !isReadOnly)
        {
            return Conflict(new
            {
                message = "Uzytkownik jest juz zalogowany na innym urzadzeniu. Czy chcesz go wylogowac i zalogowac sie tutaj?",
                requiresForce = true
            });
        }

        if (!isReadOnly)
        {
            int activeTotal = await _db.UserSessions.CountAsync(s => s.LogoutAtUtc == null);
            if (activeSession == null && activeTotal >= (license.Seats ?? 1))
            {
                return BadRequest(new { message = "Limit stanowisk w Twojej licencji zostal wyczerpany." });
            }
        }

        if (req.Force && activeSession != null)
        {
            await CloseActiveSessions(user.Id);
        }

        return GenerateToken(user.Id, user.Login, false, req.MachineName, req.IpAddress, saveSession: true, isReadOnly: isReadOnly);
    }

    private IActionResult GenerateToken(Guid userId, string login, bool isSystemAdmin, string? machineName, string? ipAddress, bool saveSession, bool isReadOnly)
    {
        string sessionId = Guid.NewGuid().ToString();

        if (saveSession)
        {
            var session = new UserSession
            {
                UserId = userId,
                LoginAtUtc = DateTime.UtcNow,
                IpAddress = ipAddress ?? HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers["User-Agent"].ToString(),
                InstanceId = _runtimeInstance.InstanceId,
                MachineName = machineName
            };
            _db.UserSessions.Add(session);
            _db.SaveChanges();
            sessionId = session.Id.ToString();
        }

        var key = JwtKeyVault.CurrentKey;
        var expires = DateTime.UtcNow.AddHours(8);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, login),
            new Claim("session_id", sessionId),
            new Claim("is_system_admin", isSystemAdmin ? "true" : "false"),
            new Claim("is_read_only", isReadOnly ? "true" : "false")
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = expires,
            Path = "/"
        };
        Response.Cookies.Append("dapnet_session", tokenString, cookieOptions);

        return Ok(new
        {
            ok = true,
            is_read_only = isReadOnly,
            login = login,
            userId = userId,
            expiresAt = ((DateTimeOffset)expires).ToUnixTimeSeconds()
        });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        Response.Cookies.Delete("dapnet_session", new CookieOptions { Path = "/", SameSite = SameSiteMode.None, Secure = true });

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out var userId)) await CloseActiveSessions(userId);
        return Ok();
    }

    [AllowAnonymous]
    [HttpPost("logout-beacon")]
    public async Task<IActionResult> LogoutBeacon([FromQuery] string token)
    {
        if (!IsRequestFromLocalMachine()) return Unauthorized();

        if (string.IsNullOrWhiteSpace(token)) return BadRequest();

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(JwtKeyVault.CurrentKey);
            var validation = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _config["Jwt:Issuer"] ?? "Platform.Api",
                ValidateAudience = true,
                ValidAudience = _config["Jwt:Audience"] ?? "Platform.Client",
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validation, out _);

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? principal.FindFirst("sub")?.Value;
            var sessionIdClaim = principal.FindFirst("session_id")?.Value;

            if (!Guid.TryParse(userIdClaim, out var userId)) return Unauthorized();
            if (!Guid.TryParse(sessionIdClaim, out var sessionId)) return Unauthorized();

            await CloseSingleSession(userId, sessionId, "Beacon");
        }
        catch { }
        return Ok();
    }

    private async Task CloseActiveSessions(Guid userId)
    {
        var activeSessions = await _db.UserSessions.Where(s => s.UserId == userId && s.LogoutAtUtc == null).ToListAsync();
        foreach (var s in activeSessions) { s.LogoutAtUtc = DateTime.UtcNow; s.LogoutReason = "Manual"; }
        await _db.SaveChangesAsync();
    }

    private async Task CloseSingleSession(Guid userId, Guid sessionId, string reason)
    {
        var session = await _db.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId && s.LogoutAtUtc == null);
        if (session is null) return;

        session.LogoutAtUtc = DateTime.UtcNow;
        session.LogoutReason = reason;
        await _db.SaveChangesAsync();
    }

    private bool IsRequestFromLocalMachine()
    {
        var ip = HttpContext.Connection.RemoteIpAddress;
        if (ip is null) return true;
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())) return true;
        return false;
    }

    private string GetServiceRateLimitKey(string login)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrWhiteSpace(ip)) ip = "local-unknown";
        return $"{login.ToLowerInvariant()}|{ip}";
    }

    private static bool TryGetServiceLockout(string key, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        if (!ServiceLoginAttempts.TryGetValue(key, out var state)) return false;

        var now = DateTimeOffset.UtcNow;
        if (state.LockedUntilUtc.HasValue && state.LockedUntilUtc.Value > now)
        {
            remaining = state.LockedUntilUtc.Value - now;
            return true;
        }

        return false;
    }

    private static bool RegisterServiceFailure(string key, int maxAttempts, TimeSpan lockoutDuration, out DateTimeOffset? retryAtUtc)
    {
        retryAtUtc = null;
        var now = DateTimeOffset.UtcNow;

        var state = ServiceLoginAttempts.AddOrUpdate(
            key,
            _ => new ServiceLoginAttemptState { FailedCount = 1 },
            (_, existing) =>
            {
                if (existing.LockedUntilUtc.HasValue && existing.LockedUntilUtc.Value <= now)
                {
                    existing.LockedUntilUtc = null;
                    existing.FailedCount = 0;
                }

                existing.FailedCount += 1;
                return existing;
            });

        if (state.FailedCount >= maxAttempts)
        {
            state.LockedUntilUtc = now.Add(lockoutDuration);
            state.FailedCount = 0;
            retryAtUtc = state.LockedUntilUtc;
            return true;
        }

        return false;
    }

    private static void ResetServiceFailures(string key)
    {
        ServiceLoginAttempts.TryRemove(key, out _);
    }

    private sealed class ServiceLoginAttemptState
    {
        public int FailedCount { get; set; }
        public DateTimeOffset? LockedUntilUtc { get; set; }
    }

}
