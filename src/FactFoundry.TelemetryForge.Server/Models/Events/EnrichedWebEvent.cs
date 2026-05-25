using System.Text.Json.Serialization;

namespace FactFoundry.TelemetryForge.Server.Models.Events;

/// <summary>
/// An enriched web session event, ready for publishing to sinks.
/// </summary>
public class EnrichedWebEvent
{
    [JsonPropertyName("site_id")]
    public string SiteId { get; set; } = string.Empty;

    [JsonPropertyName("site_name")]
    public string SiteName { get; set; } = string.Empty;

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = string.Empty;

    [JsonPropertyName("session_start")]
    public DateTime SessionStart { get; set; }

    [JsonPropertyName("session_end")]
    public DateTime SessionEnd { get; set; }

    [JsonPropertyName("duration_ms")]
    public int DurationMs { get; set; }

    [JsonPropertyName("session_hash")]
    public string SessionHash { get; set; } = string.Empty;

    [JsonPropertyName("is_first_visit")]
    public bool IsFirstVisit { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("region")]
    public string? Region { get; set; }

    [JsonPropertyName("browser")]
    public string? Browser { get; set; }

    [JsonPropertyName("os")]
    public string? Os { get; set; }

    [JsonPropertyName("device_type")]
    public string? DeviceType { get; set; }

    [JsonPropertyName("referrer")]
    public string? Referrer { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("entry_page")]
    public string EntryPage { get; set; } = string.Empty;

    [JsonPropertyName("exit_page")]
    public string ExitPage { get; set; } = string.Empty;

    [JsonPropertyName("page_path")]
    public List<string> PagePath { get; set; } = [];

    [JsonPropertyName("page_count")]
    public int PageCount { get; set; }

    [JsonPropertyName("status_codes")]
    public Dictionary<string, int> StatusCodes { get; set; } = [];
}
