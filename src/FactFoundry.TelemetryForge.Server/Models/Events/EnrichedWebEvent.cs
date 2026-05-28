using System.Text.Json.Serialization;

namespace FactFoundry.TelemetryForge.Server.Models.Events;

/// <summary>
/// An enriched web telemetry event, ready for publishing to sinks.
/// </summary>
public class EnrichedWebEvent
{
    [JsonPropertyName("site_id")]
    public string SiteId { get; set; } = string.Empty;

    [JsonPropertyName("site_name")]
    public string SiteName { get; set; } = string.Empty;

    [JsonPropertyName("session_hash")]
    public string SessionHash { get; set; } = string.Empty;

    [JsonPropertyName("is_first_visit")]
    public bool IsFirstVisit { get; set; }

    [JsonPropertyName("page")]
    public string Page { get; set; } = string.Empty;

    [JsonPropertyName("status_code")]
    public int StatusCode { get; set; }

    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = "page_view";

    [JsonPropertyName("event_name")]
    public string? EventName { get; set; }

    [JsonPropertyName("event_data")]
    public Dictionary<string, object>? EventData { get; set; }

    [JsonPropertyName("target_url")]
    public string? TargetUrl { get; set; }

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

    [JsonPropertyName("is_bot")]
    public bool IsBot { get; set; }

    [JsonPropertyName("bot_reason")]
    public string? BotReason { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }
}
