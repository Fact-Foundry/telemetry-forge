namespace FactFoundry.TelemetryForge.Server.Services;

/// <summary>
/// Fans out enriched events to multiple downstream publishers.
/// </summary>
public class CompositeEventPublisher : IEventPublisher
{
    private readonly IEnumerable<IEventPublisher> _publishers;
    private readonly ILogger<CompositeEventPublisher> _logger;

    public CompositeEventPublisher(IEnumerable<IEventPublisher> publishers, ILogger<CompositeEventPublisher> logger)
    {
        _publishers = publishers;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(T enrichedEvent, CancellationToken cancellationToken = default)
    {
        foreach (var publisher in _publishers)
        {
            try
            {
                await publisher.PublishAsync(enrichedEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sink {SinkType} failed to publish {EventType}",
                    publisher.GetType().Name, typeof(T).Name);
            }
        }
    }
}
