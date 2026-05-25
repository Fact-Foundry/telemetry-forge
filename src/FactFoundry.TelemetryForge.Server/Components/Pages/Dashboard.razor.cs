using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace FactFoundry.TelemetryForge.Server.Components.Pages;

/// <summary>
/// Dashboard page showing real-time telemetry summaries.
/// </summary>
public partial class Dashboard : ComponentBase
{
    [Inject] private TelemetryForgeDbContext Db { get; set; } = default!;

    private int _activeNow;
    private int _today;
    private int _thisWeek;
    private int _thisMonth;
    private List<SiteSummary> _sites = [];
    private List<RecentSession> _recentSessions = [];

    protected override async Task OnInitializedAsync()
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var weekStart = todayStart.AddDays(-(int)todayStart.DayOfWeek);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var activeWindow = now.AddMinutes(-5);

        _activeNow = await Db.WebEvents
            .Where(e => e.Timestamp >= activeWindow && !e.IsBot)
            .Select(e => e.SessionHash)
            .Distinct()
            .CountAsync();

        var webEvents = await Db.WebEvents.AsNoTracking()
            .Where(e => e.EventType == "page_view" && !e.IsBot)
            .GroupBy(e => new { e.SessionHash, e.SiteId })
            .Select(g => new { g.Key.SiteId, IngestedAt = g.Min(e => e.IngestedAt) })
            .ToListAsync();

        var desktopSessions = await Db.DesktopSessions.AsNoTracking().ToListAsync();
        var mobileSessions = await Db.MobileSessions.AsNoTracking().ToListAsync();

        var allTimes = webEvents.Select(s => s.IngestedAt)
            .Concat(desktopSessions.Select(s => s.IngestedAt))
            .Concat(mobileSessions.Select(s => s.IngestedAt))
            .ToList();

        _today = allTimes.Count(t => t >= todayStart);
        _thisWeek = allTimes.Count(t => t >= weekStart);
        _thisMonth = allTimes.Count(t => t >= monthStart);

        var sites = await Db.Sites.AsNoTracking().ToListAsync();
        _sites = sites.Select(site =>
        {
            var siteTimes = webEvents.Where(s => s.SiteId == site.Id).Select(s => s.IngestedAt)
                .Concat(desktopSessions.Where(s => s.SiteId == site.Id).Select(s => s.IngestedAt))
                .Concat(mobileSessions.Where(s => s.SiteId == site.Id).Select(s => s.IngestedAt))
                .ToList();

            return new SiteSummary
            {
                Name = site.Name,
                Type = site.Type,
                Today = siteTimes.Count(t => t >= todayStart),
                ThisWeek = siteTimes.Count(t => t >= weekStart),
                Total = siteTimes.Count,
                LastPayloadAt = site.LastPayloadAt
            };
        }).ToList();

        var recentWebEvents = await Db.WebEvents.AsNoTracking()
            .Where(e => e.EventType == "page_view" && !e.IsBot)
            .OrderByDescending(e => e.Timestamp)
            .Take(10)
            .ToListAsync();

        _recentSessions = recentWebEvents
            .Select(e => new RecentSession { SiteName = e.SiteName, Type = SiteType.Web, Platform = e.Browser ?? "Unknown", DurationMs = 0, SessionStart = e.Timestamp, IsFirstSeen = e.IsFirstVisit, Country = e.Country, Page = e.Page })
            .Concat(desktopSessions.OrderByDescending(s => s.IngestedAt).Take(10)
                .Select(s => new RecentSession { SiteName = s.AppName, Type = SiteType.Desktop, Platform = s.Platform, DurationMs = s.DurationMs, SessionStart = s.SessionStart, IsFirstSeen = s.IsFirstInstall }))
            .Concat(mobileSessions.OrderByDescending(s => s.IngestedAt).Take(10)
                .Select(s => new RecentSession { SiteName = s.AppName, Type = SiteType.Mobile, Platform = s.Platform, DurationMs = s.DurationMs, SessionStart = s.SessionStart, IsFirstSeen = s.IsFirstInstall }))
            .OrderByDescending(s => s.SessionStart)
            .Take(10)
            .ToList();
    }

    private static Color GetTypeColor(SiteType type) => type switch
    {
        SiteType.Web => Color.Info,
        SiteType.Desktop => Color.Success,
        SiteType.Mobile => Color.Warning,
        _ => Color.Default
    };

    private static string FormatDuration(int ms) => ms switch
    {
        0 => "—",
        < 1000 => $"{ms}ms",
        < 60000 => $"{ms / 1000.0:F1}s",
        _ => $"{ms / 60000.0:F1}m"
    };

    private static string FormatLastReceived(DateTime? dt) =>
        dt.HasValue ? dt.Value.ToString("g") : "Never";

    private class SiteSummary
    {
        public string Name { get; set; } = string.Empty;
        public SiteType Type { get; set; }
        public int Today { get; set; }
        public int ThisWeek { get; set; }
        public int Total { get; set; }
        public DateTime? LastPayloadAt { get; set; }
    }

    private class RecentSession
    {
        public string SiteName { get; set; } = string.Empty;
        public SiteType Type { get; set; }
        public string Platform { get; set; } = string.Empty;
        public int DurationMs { get; set; }
        public DateTime SessionStart { get; set; }
        public bool IsFirstSeen { get; set; }
        public string? Country { get; set; }
        public string? Page { get; set; }
    }
}
