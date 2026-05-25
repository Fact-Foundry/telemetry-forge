using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FactFoundry.TelemetryForge.Server.Services;

/// <summary>
/// Background service that materializes raw WebEvents into WebSession aggregates
/// after the configured inactivity window expires.
/// </summary>
public class SessionMaterializationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionMaterializationService> _logger;

    /// <summary>
    /// How often the materialization job runs.
    /// </summary>
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Default inactivity window — a session is considered closed when no new events
    /// arrive for this duration. Configurable via the admin UI (key: Session:InactivityMinutes).
    /// </summary>
    private const int DefaultInactivityMinutes = 30;

    public SessionMaterializationService(IServiceScopeFactory scopeFactory, ILogger<SessionMaterializationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MaterializeSessions(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Session materialization failed");
            }

            await Task.Delay(RunInterval, stoppingToken);
        }
    }

    private async Task MaterializeSessions(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TelemetryForgeDbContext>();

        var inactivityMinutes = await GetInactivityMinutes(db);
        var cutoff = DateTime.UtcNow.AddMinutes(-inactivityMinutes);

        var candidateHashes = await db.WebEvents
            .Where(e => !e.Materialized)
            .GroupBy(e => new { e.SessionHash, e.SiteId })
            .Where(g => g.Max(e => e.Timestamp) < cutoff)
            .Select(g => new { g.Key.SessionHash, g.Key.SiteId })
            .ToListAsync(cancellationToken);

        if (candidateHashes.Count == 0)
            return;

        _logger.LogInformation("Materializing {Count} web sessions", candidateHashes.Count);

        foreach (var candidate in candidateHashes)
        {
            var events = await db.WebEvents
                .Where(e => e.SessionHash == candidate.SessionHash
                         && e.SiteId == candidate.SiteId
                         && !e.Materialized)
                .OrderBy(e => e.Timestamp)
                .ToListAsync(cancellationToken);

            if (events.Count == 0)
                continue;

            var pageViews = events.Where(e => e.EventType == "page_view").ToList();
            var firstEvent = events[0];

            var statusCodes = new Dictionary<string, int>();
            foreach (var e in pageViews)
            {
                var code = e.StatusCode.ToString();
                statusCodes.TryGetValue(code, out var count);
                statusCodes[code] = count + 1;
            }

            var session = new WebSession
            {
                SiteId = firstEvent.SiteId,
                SiteName = firstEvent.SiteName,
                Platform = firstEvent.Browser ?? string.Empty,
                SessionStart = events[0].Timestamp,
                SessionEnd = events[^1].Timestamp,
                DurationMs = (int)(events[^1].Timestamp - events[0].Timestamp).TotalMilliseconds,
                IsFirstVisit = events.Any(e => e.IsFirstVisit),
                Country = firstEvent.Country,
                Region = firstEvent.Region,
                Browser = firstEvent.Browser,
                Os = firstEvent.Os,
                DeviceType = firstEvent.DeviceType,
                Referrer = firstEvent.Referrer,
                Language = firstEvent.Language,
                EntryPage = pageViews.Count > 0 ? pageViews[0].Page : firstEvent.Page,
                ExitPage = pageViews.Count > 0 ? pageViews[^1].Page : firstEvent.Page,
                PageCount = pageViews.Count,
                PagePath = pageViews.Select(e => e.Page).ToList(),
                StatusCodes = statusCodes,
                IngestedAt = DateTime.UtcNow
            };

            db.WebSessions.Add(session);

            foreach (var e in events)
                e.Materialized = true;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<int> GetInactivityMinutes(TelemetryForgeDbContext db)
    {
        var setting = await db.ServerSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "Session:InactivityMinutes");

        if (setting is not null && int.TryParse(setting.Value, out var minutes) && minutes > 0)
            return minutes;

        return DefaultInactivityMinutes;
    }
}
