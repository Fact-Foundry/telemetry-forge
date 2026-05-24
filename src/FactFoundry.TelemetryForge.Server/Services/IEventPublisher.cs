namespace FactFoundry.TelemetryForge.Server.Services;

/// <summary>
/// Publishes enriched telemetry events to configured downstream sinks.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes an enriched event object to all configured sinks.
    /// </summary>
    Task PublishAsync<T>(T enrichedEvent, CancellationToken cancellationToken = default);
}
