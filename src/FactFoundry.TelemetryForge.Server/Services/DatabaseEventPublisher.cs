using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using FactFoundry.TelemetryForge.Server.Models.Events;

namespace FactFoundry.TelemetryForge.Server.Services;

/// <summary>
/// Event publisher that persists enriched telemetry events to the local database.
/// </summary>
public class DatabaseEventPublisher : IEventPublisher
{
    private readonly TelemetryForgeDbContext _db;
    private readonly ILogger<DatabaseEventPublisher> _logger;

    public DatabaseEventPublisher(TelemetryForgeDbContext db, ILogger<DatabaseEventPublisher> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(T enrichedEvent, CancellationToken cancellationToken = default)
    {
        switch (enrichedEvent)
        {
            case EnrichedWebEvent web:
                _db.WebSessions.Add(MapWebSession(web));
                break;
            case EnrichedDesktopEvent desktop:
                _db.DesktopSessions.Add(MapDesktopSession(desktop));
                break;
            case EnrichedMobileEvent mobile:
                _db.MobileSessions.Add(MapMobileSession(mobile));
                break;
            default:
                _logger.LogWarning("DatabaseEventPublisher received unknown event type: {EventType}", typeof(T).Name);
                return;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static WebSession MapWebSession(EnrichedWebEvent e) => new()
    {
        SiteId = e.SiteId,
        SiteName = e.SiteName,
        Platform = e.Platform,
        SessionStart = e.SessionStart,
        SessionEnd = e.SessionEnd,
        DurationMs = e.DurationMs,
        SessionHash = e.SessionHash,
        IsFirstVisit = e.IsFirstVisit,
        Country = e.Country,
        Region = e.Region,
        Browser = e.Browser,
        Os = e.Os,
        DeviceType = e.DeviceType,
        Referrer = e.Referrer,
        Language = e.Language,
        EntryPage = e.EntryPage,
        ExitPage = e.ExitPage,
        PageCount = e.PageCount,
        IngestedAt = DateTime.UtcNow
    };

    private static DesktopSession MapDesktopSession(EnrichedDesktopEvent e) => new()
    {
        SiteId = e.AppId,
        AppName = e.AppName,
        AppVersion = e.AppVersion,
        Platform = e.Platform,
        OsVersion = e.OsVersion,
        FingerprintHash = e.FingerprintHash,
        IsFirstInstall = e.IsFirstInstall,
        LicenseTier = e.LicenseTier,
        SessionStart = e.SessionStart,
        SessionEnd = e.SessionEnd,
        DurationMs = e.DurationMs,
        FeatureCount = e.FeaturePath.Count,
        ErrorCount = e.ErrorEvents.Count,
        IngestedAt = DateTime.UtcNow
    };

    private static MobileSession MapMobileSession(EnrichedMobileEvent e) => new()
    {
        SiteId = e.AppId,
        AppName = e.AppName,
        AppVersion = e.AppVersion,
        Platform = e.Platform,
        OsVersion = e.OsVersion,
        DeviceHash = e.DeviceHash,
        DeviceHashType = e.DeviceHashType,
        IsFirstInstall = e.IsFirstInstall,
        SessionStart = e.SessionStart,
        SessionEnd = e.SessionEnd,
        DurationMs = e.DurationMs,
        FeatureCount = e.FeaturePath.Count,
        ErrorCount = e.ErrorEvents.Count,
        IngestedAt = DateTime.UtcNow
    };
}
