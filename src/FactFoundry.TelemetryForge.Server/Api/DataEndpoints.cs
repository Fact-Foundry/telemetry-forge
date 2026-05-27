using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace FactFoundry.TelemetryForge.Server.Api;

/// <summary>
/// Read-only API endpoints for querying telemetry data, secured by data API keys.
/// </summary>
public static class DataEndpoints
{
    /// <summary>
    /// Maps all data query endpoints under /api/data.
    /// </summary>
    public static void MapDataEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/data")
            .AddEndpointFilter<DataApiKeyValidationFilter>();

        group.MapGet("/web-events", (Delegate)GetWebEvents);
        group.MapGet("/desktop-sessions", (Delegate)GetDesktopSessions);
        group.MapGet("/mobile-sessions", (Delegate)GetMobileSessions);
    }

    private static async Task<IResult> GetWebEvents(
        HttpContext context,
        TelemetryForgeDbContext db,
        AuthService authService,
        string? siteId = null,
        DateTime? from = null,
        DateTime? to = null,
        bool? isBot = null,
        string? eventType = null,
        int page = 1,
        int pageSize = 100)
    {
        var authorizedSiteIds = GetAuthorizedSiteIds(context);
        var tz = await GetApiTimezoneAsync(authService);
        pageSize = Math.Clamp(pageSize, 1, 1000);
        page = Math.Max(1, page);

        var query = db.WebEvents.AsNoTracking()
            .Where(e => authorizedSiteIds.Contains(e.SiteId));

        if (!string.IsNullOrEmpty(siteId))
        {
            if (!authorizedSiteIds.Contains(siteId))
                return Results.Json(new { error = "Site not authorized for this key." }, statusCode: 403);
            query = query.Where(e => e.SiteId == siteId);
        }

        if (from.HasValue)
            query = query.Where(e => e.Timestamp >= new DateTimeOffset(from.Value, TimeSpan.Zero));
        if (to.HasValue)
            query = query.Where(e => e.Timestamp <= new DateTimeOffset(to.Value, TimeSpan.Zero));
        if (isBot.HasValue)
            query = query.Where(e => e.IsBot == isBot.Value);
        if (!string.IsNullOrEmpty(eventType))
            query = query.Where(e => e.EventType == eventType);

        var totalCount = await query.CountAsync();
        var events = await query
            .OrderByDescending(e => e.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var projected = events.Select(e => new
        {
            e.Id,
            e.SiteId,
            e.SiteName,
            e.SessionHash,
            e.IsFirstVisit,
            e.Page,
            e.StatusCode,
            e.EventType,
            e.EventName,
            e.EventData,
            e.TargetUrl,
            e.Country,
            e.Region,
            e.Browser,
            e.Os,
            e.DeviceType,
            e.IsBot,
            e.Referrer,
            e.Language,
            Timestamp = ConvertTime(e.Timestamp.UtcDateTime, tz),
            IngestedAt = ConvertTime(e.IngestedAt, tz)
        });

        return Results.Ok(new { totalCount, page, pageSize, data = projected });
    }

    private static async Task<IResult> GetDesktopSessions(
        HttpContext context,
        TelemetryForgeDbContext db,
        AuthService authService,
        string? siteId = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 100)
    {
        var authorizedSiteIds = GetAuthorizedSiteIds(context);
        var tz = await GetApiTimezoneAsync(authService);
        pageSize = Math.Clamp(pageSize, 1, 1000);
        page = Math.Max(1, page);

        var query = db.DesktopSessions.AsNoTracking()
            .Where(s => authorizedSiteIds.Contains(s.SiteId));

        if (!string.IsNullOrEmpty(siteId))
        {
            if (!authorizedSiteIds.Contains(siteId))
                return Results.Json(new { error = "Site not authorized for this key." }, statusCode: 403);
            query = query.Where(s => s.SiteId == siteId);
        }

        if (from.HasValue)
            query = query.Where(s => s.SessionStart >= from.Value);
        if (to.HasValue)
            query = query.Where(s => s.SessionStart <= to.Value);

        var totalCount = await query.CountAsync();
        var sessions = await query
            .OrderByDescending(s => s.SessionStart)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var projected = sessions.Select(s => new
        {
            s.Id,
            s.SiteId,
            s.AppName,
            s.AppVersion,
            s.Platform,
            s.OsVersion,
            s.IsFirstInstall,
            SessionStart = ConvertTime(s.SessionStart, tz),
            SessionEnd = ConvertTime(s.SessionEnd, tz),
            s.DurationMs,
            s.FeaturePath,
            s.FeatureCount,
            s.ErrorEvents,
            s.ErrorCount,
            IngestedAt = ConvertTime(s.IngestedAt, tz)
        });

        return Results.Ok(new { totalCount, page, pageSize, data = projected });
    }

    private static async Task<IResult> GetMobileSessions(
        HttpContext context,
        TelemetryForgeDbContext db,
        AuthService authService,
        string? siteId = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 100)
    {
        var authorizedSiteIds = GetAuthorizedSiteIds(context);
        var tz = await GetApiTimezoneAsync(authService);
        pageSize = Math.Clamp(pageSize, 1, 1000);
        page = Math.Max(1, page);

        var query = db.MobileSessions.AsNoTracking()
            .Where(s => authorizedSiteIds.Contains(s.SiteId));

        if (!string.IsNullOrEmpty(siteId))
        {
            if (!authorizedSiteIds.Contains(siteId))
                return Results.Json(new { error = "Site not authorized for this key." }, statusCode: 403);
            query = query.Where(s => s.SiteId == siteId);
        }

        if (from.HasValue)
            query = query.Where(s => s.SessionStart >= from.Value);
        if (to.HasValue)
            query = query.Where(s => s.SessionStart <= to.Value);

        var totalCount = await query.CountAsync();
        var sessions = await query
            .OrderByDescending(s => s.SessionStart)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var projected = sessions.Select(s => new
        {
            s.Id,
            s.SiteId,
            s.AppName,
            s.AppVersion,
            s.Platform,
            s.OsVersion,
            s.DeviceHashType,
            s.IsFirstInstall,
            SessionStart = ConvertTime(s.SessionStart, tz),
            SessionEnd = ConvertTime(s.SessionEnd, tz),
            s.DurationMs,
            s.FeaturePath,
            s.FeatureCount,
            s.ErrorEvents,
            s.ErrorCount,
            IngestedAt = ConvertTime(s.IngestedAt, tz)
        });

        return Results.Ok(new { totalCount, page, pageSize, data = projected });
    }

    private static List<string> GetAuthorizedSiteIds(HttpContext context)
    {
        return (List<string>)context.Items[DataApiKeyValidationFilter.AuthorizedSiteIdsKey]!;
    }

    private static async Task<TimeZoneInfo> GetApiTimezoneAsync(AuthService authService)
    {
        var tzId = await authService.GetServerSettingAsync("Api:Timezone");
        if (!string.IsNullOrEmpty(tzId))
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(tzId); }
            catch (TimeZoneNotFoundException) { }
        }
        return TimeZoneInfo.Utc;
    }

    private static string ConvertTime(DateTime utc, TimeZoneInfo tz)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(utc, tz).ToString("o");
    }
}
