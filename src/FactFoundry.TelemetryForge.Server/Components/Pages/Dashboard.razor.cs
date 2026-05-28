using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using FactFoundry.TelemetryForge.Server.Services;
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
    [Inject] private AuthService AuthService { get; set; } = default!;

    private TimeZoneInfo _tz = TimeZoneInfo.Utc;
    private int _activeNow;
    private int _today;
    private int _thisWeek;
    private int _thisMonth;
    private string _avgDuration = "—";
    private string _sitePeriod = "Today";
    private List<SiteSummary> _sites = [];
    private List<RecentSession> _recentSessions = [];

    private List<SiteSessionData> _sessionData = [];
    private List<SiteBotData> _botData = [];
    private List<Site> _allSites = [];
    private List<DesktopSession> _desktopSessions = [];
    private List<MobileSession> _mobileSessions = [];
    private List<WebSession> _webSessions = [];

    protected override async Task OnInitializedAsync()
    {
        var tzId = await AuthService.GetServerSettingAsync("Display:Timezone");
        if (!string.IsNullOrEmpty(tzId))
        {
            try { _tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
            catch (TimeZoneNotFoundException) { }
        }

        var nowUtc = DateTime.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, _tz);
        var todayStart = TimeZoneInfo.ConvertTimeToUtc(nowLocal.Date, _tz);
        var weekStart = TimeZoneInfo.ConvertTimeToUtc(nowLocal.Date.AddDays(-(int)nowLocal.DayOfWeek), _tz);
        var monthStart = TimeZoneInfo.ConvertTimeToUtc(new DateTime(nowLocal.Year, nowLocal.Month, 1), _tz);
        var activeWindow = (DateTimeOffset)nowUtc.AddMinutes(-5);

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
        _webSessions = await Db.WebSessions.AsNoTracking().ToListAsync();

        var allTimes = _sessionData.Select(s => s.IngestedAt)
            .Concat(_desktopSessions.Select(s => s.IngestedAt))
            .Concat(_mobileSessions.Select(s => s.IngestedAt))
            .ToList();

        _today = allTimes.Count(t => t >= todayStart);
        _thisWeek = allTimes.Count(t => t >= weekStart);
        _thisMonth = allTimes.Count(t => t >= monthStart);

        var avgMs = await Db.WebSessions.AsNoTracking()
            .Where(s => s.SessionStart >= weekStart && s.DurationMs > 0)
            .Select(s => (double?)s.DurationMs)
            .AverageAsync() ?? 0;
        _avgDuration = FormatDuration((int)avgMs);

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
                .Select(s => new RecentSession { SiteName = s.AppName, Type = SiteType.Desktop, Platform = s.Platform, DurationMs = s.DurationMs, SessionStart = s.SessionStart.UtcDateTime, IsFirstSeen = s.IsFirstInstall }))
            .Concat(_mobileSessions.OrderByDescending(s => s.IngestedAt).Take(10)
                .Select(s => new RecentSession { SiteName = s.AppName, Type = SiteType.Mobile, Platform = s.Platform, DurationMs = s.DurationMs, SessionStart = s.SessionStart.UtcDateTime, IsFirstSeen = s.IsFirstInstall }))
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

            var durationSources = _webSessions
                .Where(s => s.SiteId == site.Id && s.IngestedAt >= cutoff && s.DurationMs > 0)
                .Select(s => s.DurationMs)
                .Concat(_desktopSessions.Where(s => s.SiteId == site.Id && s.IngestedAt >= cutoff && s.DurationMs > 0).Select(s => s.DurationMs))
                .Concat(_mobileSessions.Where(s => s.SiteId == site.Id && s.IngestedAt >= cutoff && s.DurationMs > 0).Select(s => s.DurationMs))
                .ToList();

            var avgDurationMs = durationSources.Count > 0 ? (int)durationSources.Average() : 0;

            return new SiteSummary
            {
                SiteId = site.Id,
                Name = site.Name,
                Type = site.Type,
                Sessions = sessions,
                Bots = bots,
                AvgDurationMs = avgDurationMs,
                LastPayloadAt = site.LastPayloadAt
            };
        }).ToList();
    }

    private void OnPeriodChanged(string period)
    {
        _sitePeriod = period;
        BuildSiteSummaries();
    }

    private DateTime GetPeriodCutoff(string period)
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz);
        var localDate = period switch
        {
            "Today" => nowLocal.Date,
            "This Week" => nowLocal.Date.AddDays(-(int)nowLocal.DayOfWeek),
            "Last 30 Days" => nowLocal.Date.AddDays(-30),
            "Last 90 Days" => nowLocal.Date.AddDays(-90),
            "Last Year" => nowLocal.Date.AddYears(-1),
            _ => nowLocal.Date
        };
        return TimeZoneInfo.ConvertTimeToUtc(localDate, _tz);
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

    private string FormatTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc, _tz).ToString("M/d/yyyy H:mm");

    private string FormatLastReceived(DateTime? dt) =>
        dt.HasValue ? FormatTime(dt.Value) : "Never";

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
        public string SiteId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public SiteType Type { get; set; }
        public int Sessions { get; set; }
        public int Bots { get; set; }
        public int Total => Sessions + Bots;
        public int AvgDurationMs { get; set; }
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
