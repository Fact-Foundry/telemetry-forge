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
    private string _sitePeriod = "Today";
    private List<SiteSummary> _sites = [];
    private List<RecentSession> _recentSessions = [];

    private List<SiteSessionData> _sessionData = [];
    private List<SiteBotData> _botData = [];
    private List<Site> _allSites = [];
    private List<DesktopSession> _desktopSessions = [];
    private List<MobileSession> _mobileSessions = [];

    protected override async Task OnInitializedAsync()
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var weekStart = todayStart.AddDays(-(int)todayStart.DayOfWeek);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var activeWindow = (DateTimeOffset)now.AddMinutes(-5);

        _activeNow = await Db.WebEvents
            .Where(e => e.Timestamp >= activeWindow && !e.IsBot)
            .Select(e => e.SessionHash)
            .Distinct()
            .CountAsync();

        _sessionData = await Db.WebEvents.AsNoTracking()
            .Where(e => e.EventType == "page_view" && !e.IsBot)
            .GroupBy(e => new { e.SessionHash, e.SiteId })
            .Select(g => new SiteSessionData { SiteId = g.Key.SiteId, IngestedAt = g.Min(e => e.IngestedAt) })
            .ToListAsync();

        _botData = await Db.WebEvents.AsNoTracking()
            .Where(e => e.IsBot)
            .GroupBy(e => new { e.SessionHash, e.SiteId })
            .Select(g => new SiteBotData { SiteId = g.Key.SiteId, IngestedAt = g.Min(e => e.IngestedAt) })
            .ToListAsync();

        _desktopSessions = await Db.DesktopSessions.AsNoTracking().ToListAsync();
        _mobileSessions = await Db.MobileSessions.AsNoTracking().ToListAsync();

        var allTimes = _sessionData.Select(s => s.IngestedAt)
            .Concat(_desktopSessions.Select(s => s.IngestedAt))
            .Concat(_mobileSessions.Select(s => s.IngestedAt))
            .ToList();

        _today = allTimes.Count(t => t >= todayStart);
        _thisWeek = allTimes.Count(t => t >= weekStart);
        _thisMonth = allTimes.Count(t => t >= monthStart);

        _allSites = await Db.Sites.AsNoTracking().ToListAsync();

        BuildSiteSummaries();

        var recentWebEvents = await Db.WebEvents.AsNoTracking()
            .Where(e => e.EventType == "page_view" && !e.IsBot)
            .OrderByDescending(e => e.Timestamp)
            .Take(10)
            .ToListAsync();

        _recentSessions = recentWebEvents
            .Select(e => new RecentSession { SiteName = e.SiteName, Type = SiteType.Web, Platform = e.Browser ?? "Unknown", DurationMs = 0, SessionStart = e.Timestamp.UtcDateTime, IsFirstSeen = e.IsFirstVisit, Country = e.Country, Page = e.Page })
            .Concat(_desktopSessions.OrderByDescending(s => s.IngestedAt).Take(10)
                .Select(s => new RecentSession { SiteName = s.AppName, Type = SiteType.Desktop, Platform = s.Platform, DurationMs = s.DurationMs, SessionStart = s.SessionStart, IsFirstSeen = s.IsFirstInstall }))
            .Concat(_mobileSessions.OrderByDescending(s => s.IngestedAt).Take(10)
                .Select(s => new RecentSession { SiteName = s.AppName, Type = SiteType.Mobile, Platform = s.Platform, DurationMs = s.DurationMs, SessionStart = s.SessionStart, IsFirstSeen = s.IsFirstInstall }))
            .OrderByDescending(s => s.SessionStart)
            .Take(10)
            .ToList();
    }

    private void BuildSiteSummaries()
    {
        var cutoff = GetPeriodCutoff(_sitePeriod);

        _sites = _allSites.Select(site =>
        {
            var sessions = _sessionData.Count(s => s.SiteId == site.Id && s.IngestedAt >= cutoff)
                + _desktopSessions.Count(s => s.SiteId == site.Id && s.IngestedAt >= cutoff)
                + _mobileSessions.Count(s => s.SiteId == site.Id && s.IngestedAt >= cutoff);

            var bots = _botData.Count(b => b.SiteId == site.Id && b.IngestedAt >= cutoff);

            return new SiteSummary
            {
                Name = site.Name,
                Type = site.Type,
                Sessions = sessions,
                Bots = bots,
                LastPayloadAt = site.LastPayloadAt
            };
        }).ToList();
    }

    private void OnPeriodChanged(string period)
    {
        _sitePeriod = period;
        BuildSiteSummaries();
    }

    private static DateTime GetPeriodCutoff(string period)
    {
        var now = DateTime.UtcNow;
        return period switch
        {
            "Today" => now.Date,
            "This Week" => now.Date.AddDays(-(int)now.DayOfWeek),
            "Last 30 Days" => now.Date.AddDays(-30),
            "Last 90 Days" => now.Date.AddDays(-90),
            "Last Year" => now.Date.AddYears(-1),
            _ => now.Date
        };
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

    private class SiteSessionData
    {
        public string SiteId { get; set; } = string.Empty;
        public DateTime IngestedAt { get; set; }
    }

    private class SiteBotData
    {
        public string SiteId { get; set; } = string.Empty;
        public DateTime IngestedAt { get; set; }
    }

    private class SiteSummary
    {
        public string Name { get; set; } = string.Empty;
        public SiteType Type { get; set; }
        public int Sessions { get; set; }
        public int Bots { get; set; }
        public int Total => Sessions + Bots;
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
