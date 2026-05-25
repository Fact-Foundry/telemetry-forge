using System.Security.Cryptography;
using System.Text;
using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using FactFoundry.TelemetryForge.Server.Models.Events;
using FactFoundry.TelemetryForge.Server.Models.Payloads;
using FactFoundry.TelemetryForge.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace FactFoundry.TelemetryForge.Server.Api;

/// <summary>
/// Minimal API endpoint definitions for telemetry ingestion.
/// </summary>
public static class TelemetryEndpoints
{
    /// <summary>
    /// Maps all telemetry ingestion endpoints.
    /// </summary>
    public static void MapTelemetryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/telemetry")
            .RequireRateLimiting("telemetry")
            .AddEndpointFilter<ApiKeyValidationFilter>();

        group.MapPost("/web", (Delegate)HandleWebPayload);
        group.MapPost("/desktop", (Delegate)HandleDesktopPayload);
        group.MapPost("/mobile", (Delegate)HandleMobilePayload);
    }

    private static async Task<IResult> HandleWebPayload(
        HttpContext context,
        WebEventPayload payload,
        TelemetryForgeDbContext db,
        VisitorHashService visitorHashService,
        UserAgentParserService userAgentParser,
        GeoLocationService geoLocationService,
        IEventPublisher publisher,
        ILogger<WebEventPayload> logger)
    {
        var siteId = (string)context.Items[ApiKeyValidationFilter.SiteIdKey]!;

        try
        {
            var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.Id == siteId);
            if (site is null)
                return Results.Json(new { error = "Site not found." }, statusCode: 404);

            var sessionHash = HashSessionIdentity(payload.SessionId, payload.IpAddress);
            var visitorSessionHash = HashVisitorSessionIdentity(payload.SessionId, payload.IpAddress);

            var visitorHash = payload.GaValue ?? payload.IpAddress;
            var hashType = payload.GaValue is not null ? HashType.Ga : HashType.Ip;
            var isFirstVisit = !payload.Dnt && await visitorHashService.IsFirstSeenAsync(visitorHash, hashType, SiteType.Web, siteId, visitorSessionHash);

            var ua = userAgentParser.Parse(payload.UserAgent);

            string? country = payload.Country;
            string? region = payload.Region;
            if (string.IsNullOrWhiteSpace(country))
            {
                var clientIp = GeoLocationService.GetClientIp(context);
                var geo = geoLocationService.LookupDatabase(clientIp);
                country = geo.Country;
                region = geo.Region;
            }

            var isBot = ua.DeviceType == "bot" || string.IsNullOrWhiteSpace(payload.Language);

            var enriched = new EnrichedWebEvent
            {
                SiteId = siteId,
                SiteName = site.Name,
                SessionHash = sessionHash,
                IsFirstVisit = isFirstVisit,
                Page = payload.Page,
                StatusCode = payload.StatusCode,
                EventType = payload.EventType,
                EventName = payload.EventName,
                EventData = payload.EventData,
                TargetUrl = payload.TargetUrl,
                Country = country,
                Region = region,
                Browser = ua.Browser,
                Os = ua.Os,
                DeviceType = ua.DeviceType,
                IsBot = isBot,
                Referrer = payload.Referrer,
                Language = payload.Language,
                Timestamp = payload.Timestamp
            };

            await publisher.PublishAsync(enriched, context.RequestAborted);

            site = await db.Sites.FindAsync(siteId);
            if (site is not null)
            {
                site.LastPayloadAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }

            return Results.Accepted();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process web event for site {SiteId}", siteId);
            return Results.StatusCode(500);
        }
    }

    private static async Task<IResult> HandleDesktopPayload(
        HttpContext context,
        DesktopPayload payload,
        TelemetryForgeDbContext db,
        VisitorHashService visitorHashService,
        IEventPublisher publisher,
        ILogger<DesktopPayload> logger)
    {
        var siteId = (string)context.Items[ApiKeyValidationFilter.SiteIdKey]!;

        try
        {
            var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.Id == siteId);
            if (site is null)
                return Results.Json(new { error = "Site not found." }, statusCode: 404);

            var isFirstInstall = await visitorHashService.IsFirstSeenAsync(
                payload.FingerprintHash, HashType.Fingerprint, SiteType.Desktop, siteId);

            var enriched = new EnrichedDesktopEvent
            {
                AppId = siteId,
                AppName = site.Name,
                AppVersion = payload.AppVersion,
                Platform = payload.Platform,
                OsVersion = payload.OsVersion,
                FingerprintHash = payload.FingerprintHash,
                SessionId = payload.SessionId,
                Sequence = payload.Sequence,
                IsFirstInstall = isFirstInstall,
                SessionStart = payload.SessionStart,
                SessionEnd = payload.SessionEnd,
                DurationMs = payload.DurationMs,
                FeaturePath = payload.FeaturePath,
                ErrorEvents = payload.ErrorEvents
            };

            await publisher.PublishAsync(enriched, context.RequestAborted);

            site = await db.Sites.FindAsync(siteId);
            if (site is not null)
            {
                site.LastPayloadAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }

            return Results.Accepted();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process desktop payload for site {SiteId}", siteId);
            return Results.StatusCode(500);
        }
    }

    private static async Task<IResult> HandleMobilePayload(
        HttpContext context,
        MobilePayload payload,
        TelemetryForgeDbContext db,
        VisitorHashService visitorHashService,
        IEventPublisher publisher,
        ILogger<MobilePayload> logger)
    {
        var siteId = (string)context.Items[ApiKeyValidationFilter.SiteIdKey]!;

        try
        {
            var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.Id == siteId);
            if (site is null)
                return Results.Json(new { error = "Site not found." }, statusCode: 404);

            var hashType = payload.DeviceHashType switch
            {
                "vendor_id" => HashType.VendorId,
                "android_id" => HashType.AndroidId,
                _ => HashType.GeneratedGuid
            };

            var isFirstInstall = await visitorHashService.IsFirstSeenAsync(
                payload.DeviceHash, hashType, SiteType.Mobile, siteId);

            var enriched = new EnrichedMobileEvent
            {
                AppId = siteId,
                AppName = site.Name,
                AppVersion = payload.AppVersion,
                Platform = payload.Platform,
                OsVersion = payload.OsVersion,
                DeviceHash = payload.DeviceHash,
                DeviceHashType = payload.DeviceHashType,
                SessionId = payload.SessionId,
                Sequence = payload.Sequence,
                IsFirstInstall = isFirstInstall,
                SessionStart = payload.SessionStart,
                SessionEnd = payload.SessionEnd,
                DurationMs = payload.DurationMs,
                FeaturePath = payload.FeaturePath,
                ErrorEvents = payload.ErrorEvents
            };

            await publisher.PublishAsync(enriched, context.RequestAborted);

            site = await db.Sites.FindAsync(siteId);
            if (site is not null)
            {
                site.LastPayloadAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }

            return Results.Accepted();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process mobile payload for site {SiteId}", siteId);
            return Results.StatusCode(500);
        }
    }

    /// <summary>
    /// Hashes a session ID with the client IP for event grouping.
    /// Uses a "session" salt prefix so the result cannot be correlated
    /// with the visitor-scoped hash stored in the VisitorHash table.
    /// </summary>
    private static string HashSessionIdentity(string sessionId, string ipAddress)
    {
        var dailySalt = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var input = $"{sessionId}:{ipAddress}:session:{dailySalt}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Hashes a session ID with the client IP for first-visit carry-forward.
    /// No daily salt — this is an identity-scoped value that needs to match
    /// across the entire first session. Cannot be correlated with WebEvent.SessionHash
    /// because that hash includes a daily salt prefix.
    /// </summary>
    private static string HashVisitorSessionIdentity(string sessionId, string ipAddress)
    {
        var input = $"{sessionId}:{ipAddress}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash);
    }
}
