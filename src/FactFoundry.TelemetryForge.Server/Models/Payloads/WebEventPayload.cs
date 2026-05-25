using System.Text.Json.Serialization;

namespace FactFoundry.TelemetryForge.Server.Models.Payloads;

/// <summary>
/// Incoming per-request telemetry event from a web application.
/// </summary>
public class WebEventPayload
{
    /// <summary>
    /// Raw visitor IP address. Hashed server-side with a daily-rotating salt, then discarded.
    /// </summary>
    [JsonPropertyName("ip_address")]
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Google Analytics cookie value, if present. Hashed server-side for cross-session identity.
    /// </summary>
    [JsonPropertyName("ga_value")]
    public string? GaValue { get; set; }

    /// <summary>
    /// Raw User-Agent header string.
    /// </summary>
    [JsonPropertyName("user_agent")]
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// HTTP referrer, if any.
    /// </summary>
    [JsonPropertyName("referrer")]
    public string? Referrer { get; set; }

    /// <summary>
    /// Browser language preference from Accept-Language header.
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Page path visited or where the event occurred.
    /// </summary>
    [JsonPropertyName("page")]
    public string Page { get; set; } = string.Empty;

    /// <summary>
    /// HTTP response status code (for page_view events).
    /// </summary>
    [JsonPropertyName("status_code")]
    public int StatusCode { get; set; }

    /// <summary>
    /// Type of event: page_view, custom, link_click, or circuit_close.
    /// </summary>
    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = "page_view";

    /// <summary>
    /// Developer-defined event name (only for custom events).
    /// </summary>
    [JsonPropertyName("event_name")]
    public string? EventName { get; set; }

    /// <summary>
    /// Developer-defined key-value data (only for custom events).
    /// </summary>
    [JsonPropertyName("event_data")]
    public Dictionary<string, object>? EventData { get; set; }

    /// <summary>
    /// Href of the clicked link (only for link_click events).
    /// </summary>
    [JsonPropertyName("target_url")]
    public string? TargetUrl { get; set; }

    /// <summary>
    /// When this event occurred.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Whether the visitor has Do Not Track enabled.
    /// </summary>
    [JsonPropertyName("dnt")]
    public bool Dnt { get; set; }
}
