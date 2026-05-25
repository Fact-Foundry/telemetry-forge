namespace FactFoundry.TelemetryForge.Server.Data.Entities;

/// <summary>
/// Key-value configuration settings stored in the database, managed from the admin UI.
/// Used for server settings, OIDC configuration, retention policies, etc.
/// </summary>
public class ServerSetting
{
    /// <summary>
    /// The setting key (e.g., "Server:Name", "Oidc:Authority").
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The setting value. Sensitive values (e.g., OIDC client secret) are encrypted at rest.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Whether this setting's value is encrypted.
    /// </summary>
    public bool IsEncrypted { get; set; }
}
