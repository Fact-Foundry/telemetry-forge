namespace FactFoundry.TelemetryForge.Server.Data.Entities;

/// <summary>
/// A registered site or application that sends telemetry to this server.
/// </summary>
public class Site
{
    /// <summary>
    /// Unique identifier for this site, generated on registration.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name for the site or application.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The type of application: web, desktop, or mobile.
    /// </summary>
    public SiteType Type { get; set; }

    /// <summary>
    /// The domain of the site (e.g. "kevinoftech.com"), used for filtering self-referrals in analytics.
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// Bcrypt hash of the issued API key. The raw key is never stored.
    /// </summary>
    public string ApiKeyHash { get; set; } = string.Empty;

    /// <summary>
    /// When this site was registered.
    /// </summary>
    public DateTime RegisteredAt { get; set; }

    /// <summary>
    /// When the last telemetry payload was received from this site.
    /// </summary>
    public DateTime? LastPayloadAt { get; set; }
}

/// <summary>
/// The type of application sending telemetry.
/// </summary>
public enum SiteType
{
    /// <summary>ASP.NET or Blazor web application.</summary>
    Web,

    /// <summary>.NET desktop application (WPF, WinForms, MAUI desktop, Photino).</summary>
    Desktop,

    /// <summary>MAUI mobile application (iOS, Android).</summary>
    Mobile
}
