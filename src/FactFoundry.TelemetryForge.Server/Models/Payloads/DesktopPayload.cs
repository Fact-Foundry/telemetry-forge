using System.Text.Json.Serialization;

namespace FactFoundry.TelemetryForge.Server.Models.Payloads;

/// <summary>
/// Incoming telemetry payload from a desktop application (FactFoundry.TelemetryForge.Desktop).
/// </summary>
public class DesktopPayload
{
    [JsonPropertyName("app_name")]
    public string AppName { get; set; } = string.Empty;

    [JsonPropertyName("app_version")]
    public string AppVersion { get; set; } = string.Empty;

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = string.Empty;

    [JsonPropertyName("os_version")]
    public string OsVersion { get; set; } = string.Empty;

    [JsonPropertyName("fingerprint_hash")]
    public string FingerprintHash { get; set; } = string.Empty;

    /// <summary>
    /// Client-generated UUID, stable for the lifetime of the app session. Used to group heartbeats.
    /// </summary>
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Monotonically increasing counter (0 for first heartbeat). Detects gaps or reordering.
    /// </summary>
    [JsonPropertyName("sequence")]
    public int Sequence { get; set; }

    [JsonPropertyName("session_start")]
    public DateTimeOffset SessionStart { get; set; }

    [JsonPropertyName("session_end")]
    public DateTimeOffset SessionEnd { get; set; }

    [JsonPropertyName("duration_ms")]
    public int DurationMs { get; set; }

    [JsonPropertyName("feature_path")]
    public List<string> FeaturePath { get; set; } = [];

    [JsonPropertyName("error_events")]
    public List<ErrorEvent> ErrorEvents { get; set; } = [];
}
