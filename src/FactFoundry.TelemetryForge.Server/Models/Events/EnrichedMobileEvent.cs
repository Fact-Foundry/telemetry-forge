using System.Text.Json.Serialization;
using FactFoundry.TelemetryForge.Server.Models.Payloads;

namespace FactFoundry.TelemetryForge.Server.Models.Events;

/// <summary>
/// An enriched mobile session event, ready for publishing to sinks.
/// </summary>
public class EnrichedMobileEvent
{
    [JsonPropertyName("app_id")]
    public string AppId { get; set; } = string.Empty;

    [JsonPropertyName("app_name")]
    public string AppName { get; set; } = string.Empty;

    [JsonPropertyName("app_version")]
    public string AppVersion { get; set; } = string.Empty;

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = string.Empty;

    [JsonPropertyName("os_version")]
    public string OsVersion { get; set; } = string.Empty;

    [JsonPropertyName("device_hash")]
    public string DeviceHash { get; set; } = string.Empty;

    [JsonPropertyName("device_hash_type")]
    public string DeviceHashType { get; set; } = string.Empty;

    [JsonPropertyName("is_first_install")]
    public bool IsFirstInstall { get; set; }

    [JsonPropertyName("session_start")]
    public DateTime SessionStart { get; set; }

    [JsonPropertyName("session_end")]
    public DateTime SessionEnd { get; set; }

    [JsonPropertyName("duration_ms")]
    public int DurationMs { get; set; }

    [JsonPropertyName("feature_path")]
    public List<string> FeaturePath { get; set; } = [];

    [JsonPropertyName("error_events")]
    public List<ErrorEvent> ErrorEvents { get; set; } = [];
}
