using System.Text.Json.Serialization;

namespace FactFoundry.TelemetryForge.Server.Models.Payloads;

/// <summary>
/// Incoming telemetry payload from a web application (FactFoundry.TelemetryForge.Web).
/// </summary>
public class WebPayload
{
    [JsonPropertyName("platform")]
    public string Platform { get; set; } = string.Empty;

    [JsonPropertyName("session_start")]
    public DateTime SessionStart { get; set; }

    [JsonPropertyName("session_end")]
    public DateTime SessionEnd { get; set; }

    [JsonPropertyName("duration_ms")]
    public int DurationMs { get; set; }

    [JsonPropertyName("ip_address")]
    public string IpAddress { get; set; } = string.Empty;

    [JsonPropertyName("ga_value")]
    public string? GaValue { get; set; }

    [JsonPropertyName("user_agent")]
    public string UserAgent { get; set; } = string.Empty;

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

    [JsonPropertyName("status_codes")]
    public Dictionary<string, int> StatusCodes { get; set; } = [];

    [JsonPropertyName("dnt")]
    public bool Dnt { get; set; }
}
