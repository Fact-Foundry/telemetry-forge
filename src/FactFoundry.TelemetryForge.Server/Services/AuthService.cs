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
    /// Creates an additional admin account.
    /// </summary>
    public async Task<AdminUser> CreateAdminAsync(string email, string displayName, string password)
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
    /// Deletes an admin account by ID. Returns false if the user was not found.
    /// </summary>
    public async Task<bool> DeleteAdminAsync(string userId)
    {
        var user = await _db.AdminUsers.FindAsync(userId);
        if (user is null)
            return false;

        _db.AdminUsers.Remove(user);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Resets an admin user's password.
    /// </summary>
    public async Task<bool> ResetPasswordAsync(string userId, string newPassword)
    {
        var user = await _db.AdminUsers.FindAsync(userId);
        if (user is null)
            return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Returns all admin user accounts.
    /// </summary>
    public async Task<List<AdminUser>> GetAdminUsersAsync()
    {
        return await _db.AdminUsers.OrderBy(u => u.Email).ToListAsync();
    }

    /// <summary>
    /// Retrieves a server setting value by key, or null if not found.
    /// </summary>
    public async Task<string?> GetServerSettingAsync(string key)
    {
        var setting = await _db.ServerSettings.FindAsync(key);
        return setting?.Value;
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

    /// <summary>
    /// Checks whether OIDC is enabled and configured.
    /// </summary>
    public async Task<bool> IsOidcEnabledAsync()
    {
        var enabled = await GetServerSettingAsync("Oidc:Enabled");
        if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
            return false;

        var authority = await GetServerSettingAsync("Oidc:Authority");
        var clientId = await GetServerSettingAsync("Oidc:ClientId");
        return !string.IsNullOrWhiteSpace(authority) && !string.IsNullOrWhiteSpace(clientId);
    }

    /// <summary>
    /// Returns the configured OIDC display name, or "SSO" as a fallback.
    /// </summary>
    public async Task<string> GetOidcDisplayNameAsync()
    {
        return await GetServerSettingAsync("Oidc:DisplayName") ?? "SSO";
    }

    /// <summary>
    /// Looks up an authorized OIDC admin by email. Returns a ClaimsPrincipal if the user
    /// is pre-authorized in AdminUsers with IsOidcUser = true, or null if not authorized.
    /// </summary>
    public async Task<ClaimsPrincipal?> AuthenticateOidcUserAsync(string email)
    {
        var user = await _db.AdminUsers.FirstOrDefaultAsync(
            u => u.Email == email && u.IsOidcUser);

        return user is null ? null : CreatePrincipal(user);
    }

    /// <summary>
    /// Creates an OIDC admin account (no password, linked by email).
    /// </summary>
    public async Task<AdminUser> CreateOidcAdminAsync(string email, string displayName)
    {
        var user = new AdminUser
        {
            Email = email,
            DisplayName = displayName,
            IsOidcUser = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.AdminUsers.Add(user);
        await _db.SaveChangesAsync();
        return user;
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
