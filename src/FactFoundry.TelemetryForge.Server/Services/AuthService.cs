using System.Security.Claims;
using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace FactFoundry.TelemetryForge.Server.Services;

/// <summary>
/// Handles local password authentication and account lockout for admin users.
/// </summary>
public class AuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly TelemetryForgeDbContext _db;

    public AuthService(TelemetryForgeDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Attempts to authenticate a user with email and password.
    /// Returns the ClaimsPrincipal on success, or null on failure.
    /// </summary>
    public async Task<ClaimsPrincipal?> AuthenticateAsync(string email, string password)
    {
        var user = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Email == email && !u.IsOidcUser);
        if (user is null || user.PasswordHash is null)
            return null;

        if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
            return null;

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
                user.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);

            await _db.SaveChangesAsync();
            return null;
        }

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        await _db.SaveChangesAsync();

        return CreatePrincipal(user);
    }

    /// <summary>
    /// Checks whether the first-run wizard needs to be shown (no admin accounts exist).
    /// </summary>
    public async Task<bool> RequiresSetupAsync()
    {
        return !await _db.AdminUsers.AnyAsync();
    }

    /// <summary>
    /// Creates the initial admin account during first-run setup.
    /// </summary>
    public async Task<AdminUser> CreateInitialAdminAsync(string email, string displayName, string password)
    {
        var user = new AdminUser
        {
            Email = email,
            DisplayName = displayName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = DateTime.UtcNow
        };

        _db.AdminUsers.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Saves a server setting to the database.
    /// </summary>
    public async Task SaveServerSettingAsync(string key, string value)
    {
        var setting = await _db.ServerSettings.FindAsync(key);
        if (setting is not null)
        {
            setting.Value = value;
        }
        else
        {
            _db.ServerSettings.Add(new ServerSetting { Key = key, Value = value });
        }
        await _db.SaveChangesAsync();
    }

    private static ClaimsPrincipal CreatePrincipal(AdminUser user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }
}
