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
    /// Visitor's country, resolved by the SDK from CloudFlare CF-IPCountry header or similar.
    /// If empty, the server will attempt a GeoIP database lookup as a fallback.
    /// </summary>
    [JsonPropertyName("country")]
    public string? Country { get; set; }

    /// <summary>
    /// Visitor's region/state, resolved by the SDK from CloudFlare CF-Region header or similar.
    /// Only available on CloudFlare paid plans. Falls back to GeoIP database if empty.
    /// </summary>
    [JsonPropertyName("region")]
    public string? Region { get; set; }

    /// <summary>
    /// Client-generated session identifier. Hashed with IP and a daily salt server-side
    /// to produce the session hash — prevents cross-day tracking via reused session IDs.
    /// </summary>
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// When this event occurred.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Whether the visitor has Do Not Track enabled.
    /// </summary>
    [JsonPropertyName("dnt")]
    public bool Dnt { get; set; }

    /// <summary>
    /// Sec-CH-UA client hint header value. Contains browser brand list (e.g., "Brave";v="130", "Chromium";v="130").
    /// </summary>
    [JsonPropertyName("sec_ch_ua")]
    public string? SecChUa { get; set; }

    /// <summary>
    /// Sec-CH-UA-Mobile client hint header value. "?1" for mobile, "?0" for non-mobile.
    /// </summary>
    [JsonPropertyName("sec_ch_ua_mobile")]
    public string? SecChUaMobile { get; set; }

    /// <summary>
    /// Sec-CH-UA-Platform client hint header value. Contains the platform (e.g., "Windows", "macOS", "Linux").
    /// </summary>
    [JsonPropertyName("sec_ch_ua_platform")]
    public string? SecChUaPlatform { get; set; }
}
