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
        WebPayload payload,
        TelemetryForgeDbContext db,
        VisitorHashService visitorHashService,
        IEventPublisher publisher,
        ILogger<WebPayload> logger)
    {
        var siteId = (string)context.Items[ApiKeyValidationFilter.SiteIdKey]!;

        try
        {
            var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.Id == siteId);
            if (site is null)
                return Results.Json(new { error = "Site not found." }, statusCode: 404);

            var visitorHash = payload.GaHash ?? payload.IpHash;
            var hashType = payload.GaHash is not null ? HashType.Ga : HashType.Ip;
            var isFirstVisit = !payload.Dnt && await visitorHashService.IsFirstSeenAsync(visitorHash, hashType, SiteType.Web, siteId);

            var enriched = new EnrichedWebEvent
            {
                SiteId = siteId,
                SiteName = site.Name,
                Platform = payload.Platform,
                SessionStart = payload.SessionStart,
                SessionEnd = payload.SessionEnd,
                DurationMs = payload.DurationMs,
                SessionHash = payload.IpHash,
                IsFirstVisit = isFirstVisit,
                Referrer = payload.Referrer,
                Language = payload.Language,
                EntryPage = payload.EntryPage,
                ExitPage = payload.ExitPage,
                PagePath = payload.PagePath,
                PageCount = payload.PagePath.Count,
                StatusCodes = payload.StatusCodes
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
            logger.LogError(ex, "Failed to process web payload for site {SiteId}", siteId);
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
                IsFirstInstall = isFirstInstall,
                LicenseTier = null,
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
}
