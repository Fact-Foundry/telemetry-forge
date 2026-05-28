namespace FactFoundry.TelemetryForge.Server.Data.Entities;

/// <summary>
/// An error event stored as part of a session record.
/// </summary>
public class StoredErrorEvent
{
    /// <summary>
    /// The feature where the error occurred.
    /// </summary>
    public string Feature { get; set; } = string.Empty;

    /// <summary>
    /// The error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// When the error occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }
}
