using System.Text.Json.Serialization;

namespace FactFoundry.TelemetryForge.Server.Models.Payloads;

/// <summary>
/// An error event captured during a desktop or mobile session.
/// </summary>
public class ErrorEvent
{
    [JsonPropertyName("feature")]
    public string Feature { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}
