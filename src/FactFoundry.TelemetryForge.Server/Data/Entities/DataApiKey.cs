namespace FactFoundry.TelemetryForge.Server.Data.Entities;

/// <summary>
/// An API key that grants read access to telemetry data for a scoped set of sites.
/// </summary>
public class DataApiKey
{
    /// <summary>
    /// Unique identifier for this data API key.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Human-readable label (e.g. "Sandbox", "KoT Production").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// BCrypt hash of the raw API key. The raw key is never stored.
    /// </summary>
    public string ApiKeyHash { get; set; } = string.Empty;

    /// <summary>
    /// Site IDs this key is authorized to read data from.
    /// </summary>
    public List<string> SiteIds { get; set; } = [];

    /// <summary>
    /// When this key was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
