using System.Text;
using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using FactFoundry.TelemetryForge.Server.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using MudBlazor;

namespace FactFoundry.TelemetryForge.Server.Components.Pages;

/// <summary>
/// Event Stream page showing a filterable feed of recent events with expandable detail.
/// </summary>
public partial class EventStream : ComponentBase
{
    [Inject] private TelemetryForgeDbContext Db { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "site")]
    private string? SiteQueryParam { get; set; }

    private List<Site> _sites = [];
    private List<EventRow> _events = [];
    private string _siteFilter = string.Empty;
    private string _typeFilter = string.Empty;
    private string _pageFilter = string.Empty;
    private List<string> _pageOptions = [];
    private bool _hideBots = true;
    private bool _hideIgnored;
    private TimeZoneInfo _tz = TimeZoneInfo.Utc;

    protected override async Task OnInitializedAsync()
    {
        if (!string.IsNullOrEmpty(SiteQueryParam))
            _siteFilter = SiteQueryParam;

        var tzId = await AuthService.GetServerSettingAsync("Display:Timezone");
        if (!string.IsNullOrEmpty(tzId))
        {
            try { _tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
            catch (TimeZoneNotFoundException) { }
        }

        _sites = await Db.Sites.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        await LoadEvents();
    }

    private async Task LoadEvents()
    {
        await LoadPageOptions();

        var events = new List<EventRow>();

        if (_typeFilter is "" or "Web")
        {
            var webQuery = Db.WebEvents.AsNoTracking().AsQueryable();
            if (_hideBots)
                webQuery = webQuery.Where(e => !e.IsBot);
            if (_hideIgnored)
                webQuery = webQuery.Where(e => !e.IsIgnored);
            if (!string.IsNullOrEmpty(_siteFilter))
                webQuery = webQuery.Where(e => e.SiteId == _siteFilter);
            if (!string.IsNullOrEmpty(_pageFilter))
                webQuery = webQuery.Where(e => e.Page == _pageFilter);

            var webEvents = await webQuery.OrderByDescending(e => e.Timestamp).Take(100).ToListAsync();

            var sessionHashes = webEvents.Select(e => e.SessionHash).Distinct().ToList();
            var sessionDurations = await Db.WebEvents.AsNoTracking()
                .Where(e => sessionHashes.Contains(e.SessionHash))
                .GroupBy(e => e.SessionHash)
                .Select(g => new { SessionHash = g.Key, MinTs = g.Min(e => e.Timestamp), MaxTs = g.Max(e => e.Timestamp) })
                .ToDictionaryAsync(g => g.SessionHash, g => (int)(g.MaxTs - g.MinTs).TotalMilliseconds);

            events.AddRange(webEvents.Select(e => new EventRow
            {
                Id = e.Id,
                SiteName = e.SiteName,
                SourceType = "Web",
                WebEventType = e.EventType,
                IsFirstSeen = e.IsFirstVisit,
                IsBot = e.IsBot,
                IsIgnored = e.IsIgnored,
                Platform = e.Browser ?? "Unknown",
                IngestedAt = e.IngestedAt,
                Timestamp = e.Timestamp.UtcDateTime,
                Page = e.Page,
                StatusCode = e.StatusCode,
                EventName = e.EventName,
                TargetUrl = e.TargetUrl,
                Language = e.Language,
                Referrer = e.Referrer,
                Country = e.Country,
                Region = e.Region,
                Browser = e.Browser,
                Os = e.Os,
                DeviceType = e.DeviceType,
                SessionHash = e.SessionHash,
                DurationMs = sessionDurations.GetValueOrDefault(e.SessionHash, 0),
            }));
        }

        if (_typeFilter is "" or "Desktop")
        {
            var desktopQuery = Db.DesktopSessions.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(_siteFilter))
                desktopQuery = desktopQuery.Where(s => s.SiteId == _siteFilter);

            // FeaturePath is a JSON column, so membership filtering happens in memory (after ordering).
            var desktopSessions = string.IsNullOrEmpty(_pageFilter)
                ? await desktopQuery.OrderByDescending(s => s.IngestedAt).Take(100).ToListAsync()
                : (await desktopQuery.OrderByDescending(s => s.IngestedAt).ToListAsync())
                    .Where(s => s.FeaturePath.Contains(_pageFilter)).Take(100).ToList();
            events.AddRange(desktopSessions.Select(s => new EventRow
            {
                Id = s.Id,
                SiteName = s.AppName,
                SourceType = "Desktop",
                IsFirstSeen = s.IsFirstInstall,
                Platform = s.Platform,
                DurationMs = s.DurationMs,
                IngestedAt = s.IngestedAt,
                Timestamp = s.SessionStart.UtcDateTime,
                SessionEnd = s.SessionEnd.UtcDateTime,
                AppVersion = s.AppVersion,
                OsVersion = s.OsVersion,
                FeaturePath = s.FeaturePath,
                FeatureCount = s.FeatureCount,
                ErrorEvents = s.ErrorEvents,
                ErrorCount = s.ErrorCount,
                FingerprintHash = s.FingerprintHash
            }));
        }

        if (_typeFilter is "" or "Mobile")
        {
            var mobileQuery = Db.MobileSessions.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(_siteFilter))
                mobileQuery = mobileQuery.Where(s => s.SiteId == _siteFilter);

            // FeaturePath is a JSON column, so membership filtering happens in memory (after ordering).
            var mobileSessions = string.IsNullOrEmpty(_pageFilter)
                ? await mobileQuery.OrderByDescending(s => s.IngestedAt).Take(100).ToListAsync()
                : (await mobileQuery.OrderByDescending(s => s.IngestedAt).ToListAsync())
                    .Where(s => s.FeaturePath.Contains(_pageFilter)).Take(100).ToList();
            events.AddRange(mobileSessions.Select(s => new EventRow
            {
                Id = s.Id,
                SiteName = s.AppName,
                SourceType = "Mobile",
                IsFirstSeen = s.IsFirstInstall,
                Platform = s.Platform,
                DurationMs = s.DurationMs,
                IngestedAt = s.IngestedAt,
                Timestamp = s.SessionStart.UtcDateTime,
                SessionEnd = s.SessionEnd.UtcDateTime,
                AppVersion = s.AppVersion,
                OsVersion = s.OsVersion,
                FeaturePath = s.FeaturePath,
                FeatureCount = s.FeatureCount,
                ErrorEvents = s.ErrorEvents,
                ErrorCount = s.ErrorCount,
                DeviceHash = s.DeviceHash,
                DeviceHashType = s.DeviceHashType
            }));
        }

        if (_typeFilter is "" or "Api")
        {
            var apiQuery = Db.ApiEvents.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(_siteFilter))
                apiQuery = apiQuery.Where(e => e.SiteId == _siteFilter);
            if (!string.IsNullOrEmpty(_pageFilter))
                apiQuery = apiQuery.Where(e => e.RouteTemplate == _pageFilter);

            var apiEvents = await apiQuery.OrderByDescending(e => e.IngestedAt).Take(100).ToListAsync();
            var siteNames = _sites.ToDictionary(s => s.Id, s => s.Name);

            events.AddRange(apiEvents.Select(e => new EventRow
            {
                Id = e.Id,
                SiteName = siteNames.GetValueOrDefault(e.SiteId, e.SiteId),
                SourceType = "Api",
                ApiMethod = e.Method,
                Page = e.RouteTemplate,
                StatusCode = e.StatusCode,
                DurationMs = e.LatencyMs,
                Country = e.Country,
                Outcome = e.Outcome,
                IngestedAt = e.IngestedAt,
                Timestamp = e.Timestamp.UtcDateTime,
            }));
        }

        _events = events.OrderByDescending(e => e.IngestedAt).Take(100).ToList();
    }

    private async Task ApplyFilters()
    {
        await LoadEvents();
    }

    private async Task OnPageChanged(string page)
    {
        _pageFilter = page;
        await LoadEvents();
    }

    /// <summary>
    /// Builds the distinct Page/Feature options for the filter dropdown, scoped by the active
    /// site and type (and bot/ignored toggles for web). Resets the selection if it's no longer available.
    /// </summary>
    private async Task LoadPageOptions()
    {
        var options = new HashSet<string>(StringComparer.Ordinal);

        if (_typeFilter is "" or "Web")
        {
            var webQuery = Db.WebEvents.AsNoTracking().AsQueryable();
            if (_hideBots)
                webQuery = webQuery.Where(e => !e.IsBot);
            if (_hideIgnored)
                webQuery = webQuery.Where(e => !e.IsIgnored);
            if (!string.IsNullOrEmpty(_siteFilter))
                webQuery = webQuery.Where(e => e.SiteId == _siteFilter);

            var pages = await webQuery
                .Where(e => e.Page != null && e.Page != "")
                .Select(e => e.Page!)
                .Distinct()
                .ToListAsync();
            foreach (var p in pages)
                options.Add(p);
        }

        if (_typeFilter is "" or "Desktop")
        {
            var desktopQuery = Db.DesktopSessions.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(_siteFilter))
                desktopQuery = desktopQuery.Where(s => s.SiteId == _siteFilter);

            // FeaturePath is a JSON column; flatten distinct feature names in memory.
            var sessions = await desktopQuery.ToListAsync();
            foreach (var feature in sessions.SelectMany(s => s.FeaturePath))
                if (!string.IsNullOrEmpty(feature))
                    options.Add(feature);
        }

        if (_typeFilter is "" or "Mobile")
        {
            var mobileQuery = Db.MobileSessions.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(_siteFilter))
                mobileQuery = mobileQuery.Where(s => s.SiteId == _siteFilter);

            var sessions = await mobileQuery.ToListAsync();
            foreach (var feature in sessions.SelectMany(s => s.FeaturePath))
                if (!string.IsNullOrEmpty(feature))
                    options.Add(feature);
        }

        if (_typeFilter is "" or "Api")
        {
            var apiQuery = Db.ApiEvents.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(_siteFilter))
                apiQuery = apiQuery.Where(e => e.SiteId == _siteFilter);

            var routes = await apiQuery
                .Where(e => e.RouteTemplate != null && e.RouteTemplate != "")
                .Select(e => e.RouteTemplate)
                .Distinct()
                .ToListAsync();
            foreach (var r in routes)
                options.Add(r);
        }

        _pageOptions = options.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        if (!string.IsNullOrEmpty(_pageFilter) && !_pageOptions.Contains(_pageFilter))
            _pageFilter = string.Empty;
    }

    private async Task OnHideBotsChanged(bool value)
    {
        _hideBots = value;
        await LoadEvents();
    }

    private async Task OnHideIgnoredChanged(bool value)
    {
        _hideIgnored = value;
        await LoadEvents();
    }

    private async Task ExportCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Site,Source,Event,Visitor,Page/Feature,Session,Country,Time,Browser,OS,DeviceType,Language,Referrer,StatusCode");

        foreach (var e in _events)
        {
            var eventCol = e.SourceType switch
            {
                "Web" => e.WebEventType ?? "",
                "Api" => e.ApiMethod ?? "",
                _ => "session"
            };
            var visitor = e.SourceType == "Api"
                ? ""
                : e.IsBot ? "Bot" : e.IsIgnored ? "Ignored" : e.IsFirstSeen ? "New" : "Returning";
            var pageFeature = e.SourceType switch
            {
                "Web" => e.EventName ?? e.Page ?? "",
                "Api" => e.Page ?? "",
                _ => $"{e.FeatureCount} features"
            };
            var session = e.SessionHash ?? "";

            sb.AppendLine(string.Join(",",
                Csv(e.SiteName), Csv(e.SourceType), Csv(eventCol), Csv(visitor),
                Csv(pageFeature), Csv(session), Csv(e.Country ?? ""),
                Csv(e.Timestamp.ToString("o")), Csv(e.Browser ?? ""), Csv(e.Os ?? ""),
                Csv(e.DeviceType ?? ""), Csv(e.Language ?? ""), Csv(e.Referrer ?? ""),
                e.StatusCode > 0 ? e.StatusCode.ToString() : ""));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var base64 = Convert.ToBase64String(bytes);
        await Js.InvokeVoidAsync("eval",
            $"(() => {{ const a = document.createElement('a'); a.href = 'data:text/csv;base64,{base64}'; a.download = 'telemetry-events.csv'; a.click(); }})()");
    }

    private static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static Color GetTypeColor(string type) => type switch
    {
        "Web" => Color.Info,
        "Desktop" => Color.Success,
        "Mobile" => Color.Warning,
        "Api" => Color.Primary,
        _ => Color.Default
    };

    private static Color GetWebEventColor(string? eventType) => eventType switch
    {
        "page_view" => Color.Info,
        "custom" => Color.Tertiary,
        "link_click" => Color.Warning,
        "circuit_open" => Color.Default,
        "circuit_close" => Color.Default,
        _ => Color.Default
    };

    private string FormatTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc, _tz).ToString("M/d/yyyy H:mm");

    private static string FormatDuration(int ms) => ms switch
    {
        0 => "—",
        < 1000 => $"{ms}ms",
        < 60000 => $"{ms / 1000.0:F1}s",
        _ => $"{ms / 60000.0:F1}m"
    };

    private class EventRow
    {
        public long Id { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public string SourceType { get; set; } = string.Empty;
        public string? WebEventType { get; set; }
        public bool IsFirstSeen { get; set; }
        public string Platform { get; set; } = string.Empty;
        public int DurationMs { get; set; }
        public DateTime IngestedAt { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime? SessionEnd { get; set; }
        public bool Expanded { get; set; }

        public bool IsBot { get; set; }
        public bool IsIgnored { get; set; }

        // Web event fields
        public string? SessionHash { get; set; }
        public string? Page { get; set; }
        public int StatusCode { get; set; }
        public string? EventName { get; set; }
        public string? TargetUrl { get; set; }
        public string? Language { get; set; }
        public string? Referrer { get; set; }
        public string? Country { get; set; }
        public string? Region { get; set; }
        public string? Browser { get; set; }
        public string? Os { get; set; }
        public string? DeviceType { get; set; }

        // API event fields
        public string? ApiMethod { get; set; }
        public string? Outcome { get; set; }

        // Desktop/Mobile session fields
        public string? AppVersion { get; set; }
        public string? OsVersion { get; set; }
        public string? FingerprintHash { get; set; }
        public string? DeviceHash { get; set; }
        public string? DeviceHashType { get; set; }
        public List<string> FeaturePath { get; set; } = [];
        public int FeatureCount { get; set; }
        public List<StoredErrorEvent> ErrorEvents { get; set; } = [];
        public int ErrorCount { get; set; }
    }
}
