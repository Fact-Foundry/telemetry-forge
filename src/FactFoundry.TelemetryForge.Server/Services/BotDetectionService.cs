using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FactFoundry.TelemetryForge.Server.Services;

/// <summary>
/// Retroactive bot detection — scans existing WebEvents and applies bot heuristics.
/// </summary>
public class BotDetectionService
{
    private readonly TelemetryForgeDbContext _db;

    /// <summary>
    /// Creates a new BotDetectionService instance.
    /// </summary>
    public BotDetectionService(TelemetryForgeDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Scans all WebEvents for bot patterns and backfills BotReason on existing bots.
    /// Returns the number of newly flagged events and the number of reasons backfilled.
    /// </summary>
    public async Task<ScanResult> ScanAsync()
    {
        var newlyFlagged = 0;
        var reasonsBackfilled = 0;

        var sessionHashes = await _db.WebEvents.AsNoTracking()
            .Select(e => e.SessionHash)
            .Distinct()
            .ToListAsync();

        foreach (var sessionHash in sessionHashes)
        {
            var events = await _db.WebEvents
                .Where(e => e.SessionHash == sessionHash)
                .OrderBy(e => e.Timestamp)
                .ToListAsync();

            if (events.Count == 0) continue;

            var alreadyBot = events[0].IsBot;
            string? detectedReason = null;

            detectedReason ??= CheckPageVelocity(events);
            detectedReason ??= CheckPathScan(events);
            detectedReason ??= CheckCountryHop(events);

            if (detectedReason != null && !alreadyBot)
            {
                foreach (var evt in events)
                {
                    evt.IsBot = true;
                    evt.BotReason = detectedReason;
                }
                newlyFlagged += events.Count;
            }
            else if (alreadyBot)
            {
                var needsReason = events.Where(e => e.BotReason == null).ToList();
                if (needsReason.Count > 0)
                {
                    var reason = detectedReason ?? BackfillReason(events);
                    foreach (var evt in needsReason)
                        evt.BotReason = reason;
                    reasonsBackfilled += needsReason.Count;
                }
            }
        }

        await _db.SaveChangesAsync();

        return new ScanResult { NewlyFlagged = newlyFlagged, ReasonsBackfilled = reasonsBackfilled };
    }

    private static string? CheckPageVelocity(List<WebEvent> events)
    {
        var pageViews = events
            .Where(e => e.EventType == "page_view")
            .OrderBy(e => e.Timestamp)
            .ToList();

        for (var i = 0; i < pageViews.Count; i++)
        {
            var windowEnd = pageViews[i].Timestamp.AddSeconds(60);
            var count = pageViews.Count(pv => pv.Timestamp >= pageViews[i].Timestamp && pv.Timestamp <= windowEnd);
            if (count >= 5)
                return "page-velocity";
        }

        return null;
    }

    private static string? CheckPathScan(List<WebEvent> events)
    {
        var pages = events
            .Where(e => e.EventType == "page_view" && !string.IsNullOrEmpty(e.Page))
            .Select(e => e.Page)
            .ToList();

        var byFilename = pages
            .Select(p => new { Path = p, FileName = Path.GetFileName(p.TrimEnd('/')) })
            .Where(x => !string.IsNullOrEmpty(x.FileName) && x.FileName.Contains('.'))
            .GroupBy(x => x.FileName, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byFilename)
        {
            var distinctPaths = group.Select(x => x.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            if (distinctPaths >= 5)
                return "path-scan";
        }

        return null;
    }

    private static string? CheckCountryHop(List<WebEvent> events)
    {
        var countries = events
            .Where(e => e.Country != null)
            .Select(e => e.Country!)
            .Distinct()
            .Count();

        return countries >= 3 ? "country-hop" : null;
    }

    private static string BackfillReason(List<WebEvent> events)
    {
        var first = events.FirstOrDefault(e => e.IsBot);
        if (first == null) return "unknown";

        if (first.DeviceType == "bot")
            return "user-agent";

        var browser = first.Browser?.ToLowerInvariant() ?? "";
        if (browser.Contains("curl") || browser.Contains("wget") || browser.Contains("httpie"))
            return "user-agent";

        if (string.IsNullOrWhiteSpace(first.Language))
            return "no-language";

        return "unknown";
    }

    /// <summary>
    /// Result of a retroactive bot scan.
    /// </summary>
    public class ScanResult
    {
        /// <summary>Number of events newly flagged as bot.</summary>
        public int NewlyFlagged { get; set; }

        /// <summary>Number of existing bot events that had BotReason backfilled.</summary>
        public int ReasonsBackfilled { get; set; }
    }
}
