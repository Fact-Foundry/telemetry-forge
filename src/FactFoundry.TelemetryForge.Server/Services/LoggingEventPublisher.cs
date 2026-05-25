using System.Text.Json;

namespace FactFoundry.TelemetryForge.Server.Services;

/// <summary>
/// Development event publisher that logs enriched events to the application logger.
/// Replaced by real sink implementations (database, HTTP endpoints) in production.
/// </summary>
public class LoggingEventPublisher : IEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly ILogger<LoggingEventPublisher> _logger;

    public LoggingEventPublisher(ILogger<LoggingEventPublisher> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task PublishAsync<T>(T enrichedEvent, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(enrichedEvent, JsonOptions);
        _logger.LogInformation("Event published: {EventType} — {EventJson}", typeof(T).Name, json);
        return Task.CompletedTask;
    }
}
