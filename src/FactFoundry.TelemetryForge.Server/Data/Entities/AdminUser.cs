namespace FactFoundry.TelemetryForge.Server.Data.Entities;

/// <summary>
/// An administrator who can access the TelemetryForge admin UI.
/// Supports both local password auth and OIDC (e.g., Microsoft Entra ID).
/// </summary>
public class AdminUser
{
    /// <summary>
    /// Unique identifier for this admin user.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Email address used for login and OIDC matching.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the admin user.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Bcrypt hash of the password. Null for OIDC-only users.
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Whether this user authenticates via OIDC rather than local password.
    /// </summary>
    public bool IsOidcUser { get; set; }

    /// <summary>
    /// Number of consecutive failed login attempts. Reset on successful login.
    /// </summary>
    public int FailedLoginAttempts { get; set; }

    /// <summary>
    /// When the account is locked until due to repeated failed login attempts. Null if not locked.
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// When this admin account was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
