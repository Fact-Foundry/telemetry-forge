using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using FactFoundry.TelemetryForge.Server.Models.Events;
using Microsoft.EntityFrameworkCore;

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
                _db.WebEvents.Add(MapWebEvent(web));
                break;
            case EnrichedDesktopEvent desktop:
                await UpsertDesktopSession(desktop, cancellationToken);
                return;
            case EnrichedMobileEvent mobile:
                await UpsertMobileSession(mobile, cancellationToken);
                return;
            default:
                _logger.LogWarning("DatabaseEventPublisher received unknown event type: {EventType}", typeof(T).Name);
                return;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertDesktopSession(EnrichedDesktopEvent e, CancellationToken cancellationToken)
    {
        var existing = !string.IsNullOrEmpty(e.SessionId)
            ? await _db.DesktopSessions.FirstOrDefaultAsync(s => s.SessionId == e.SessionId, cancellationToken)
            : null;

        if (existing is not null)
        {
            existing.SessionEnd = e.SessionEnd;
            existing.DurationMs = e.DurationMs;
            existing.FeaturePath.AddRange(e.FeaturePath);
            existing.FeatureCount = existing.FeaturePath.Count;
            existing.ErrorEvents.AddRange(e.ErrorEvents.Select(err => new StoredErrorEvent
            {
                Feature = err.Feature,
                Message = err.Message,
                Timestamp = err.Timestamp
            }));
            existing.ErrorCount = existing.ErrorEvents.Count;
            existing.IngestedAt = DateTime.UtcNow;
        }
        else
        {
            _db.DesktopSessions.Add(MapDesktopSession(e));
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertMobileSession(EnrichedMobileEvent e, CancellationToken cancellationToken)
    {
        var existing = !string.IsNullOrEmpty(e.SessionId)
            ? await _db.MobileSessions.FirstOrDefaultAsync(s => s.SessionId == e.SessionId, cancellationToken)
            : null;

        if (existing is not null)
        {
            existing.SessionEnd = e.SessionEnd;
            existing.DurationMs = e.DurationMs;
            existing.FeaturePath.AddRange(e.FeaturePath);
            existing.FeatureCount = existing.FeaturePath.Count;
            existing.ErrorEvents.AddRange(e.ErrorEvents.Select(err => new StoredErrorEvent
            {
                Feature = err.Feature,
                Message = err.Message,
                Timestamp = err.Timestamp
            }));
            existing.ErrorCount = existing.ErrorEvents.Count;
            existing.IngestedAt = DateTime.UtcNow;
        }
        else
        {
            _db.MobileSessions.Add(MapMobileSession(e));
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static WebEvent MapWebEvent(EnrichedWebEvent e) => new()
    {
        SiteId = e.SiteId,
        SiteName = e.SiteName,
        SessionHash = e.SessionHash,
        IsFirstVisit = e.IsFirstVisit,
        Page = e.Page,
        StatusCode = e.StatusCode,
        EventType = e.EventType,
        EventName = e.EventName,
        EventData = e.EventData is not null
            ? System.Text.Json.JsonSerializer.Serialize(e.EventData)
            : null,
        TargetUrl = e.TargetUrl,
        Country = e.Country,
        Region = e.Region,
        Browser = e.Browser,
        Os = e.Os,
        DeviceType = e.DeviceType,
        Referrer = e.Referrer,
        Language = e.Language,
        IsBot = e.IsBot,
        Timestamp = e.Timestamp,
        IngestedAt = DateTime.UtcNow
    };

    private static DesktopSession MapDesktopSession(EnrichedDesktopEvent e) => new()
    {
        SiteId = e.AppId,
        SessionId = e.SessionId,
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
        FeaturePath = e.FeaturePath,
        ErrorEvents = e.ErrorEvents.Select(err => new StoredErrorEvent
        {
            Feature = err.Feature,
            Message = err.Message,
            Timestamp = err.Timestamp
        }).ToList(),
        IngestedAt = DateTime.UtcNow
    };

    private static MobileSession MapMobileSession(EnrichedMobileEvent e) => new()
    {
        SiteId = e.AppId,
        SessionId = e.SessionId,
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
        FeaturePath = e.FeaturePath,
        ErrorEvents = e.ErrorEvents.Select(err => new StoredErrorEvent
        {
            Feature = err.Feature,
            Message = err.Message,
            Timestamp = err.Timestamp
        }).ToList(),
        IngestedAt = DateTime.UtcNow
    };
}
