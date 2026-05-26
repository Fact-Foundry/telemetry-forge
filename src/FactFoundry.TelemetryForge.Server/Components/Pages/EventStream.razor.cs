using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace FactFoundry.TelemetryForge.Server.Components.Pages;

/// <summary>
/// Event Stream page showing a filterable feed of recent events with expandable detail.
/// </summary>
public partial class EventStream : ComponentBase
{
    [Inject] private TelemetryForgeDbContext Db { get; set; } = default!;

    private List<Site> _sites = [];
    private List<EventRow> _events = [];
    private string _siteFilter = string.Empty;
    private string _typeFilter = string.Empty;
    private bool _hideBots = true;

    protected override async Task OnInitializedAsync()
    {
        _sites = await Db.Sites.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        await LoadEvents();
    }

    private async Task LoadEvents()
    {
        var events = new List<EventRow>();

        if (_typeFilter is "" or "Web")
        {
            var webQuery = Db.WebEvents.AsNoTracking().AsQueryable();
            if (_hideBots)
                webQuery = webQuery.Where(e => !e.IsBot);
            if (!string.IsNullOrEmpty(_siteFilter))
                webQuery = webQuery.Where(e => e.SiteId == _siteFilter);

            var webEvents = await webQuery.OrderByDescending(e => e.Timestamp).Take(50).ToListAsync();
            events.AddRange(webEvents.Select(e => new EventRow
            {
                Id = e.Id,
                SiteName = e.SiteName,
                SourceType = "Web",
                WebEventType = e.EventType,
                IsFirstSeen = e.IsFirstVisit,
                IsBot = e.IsBot,
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
            }));
        }

        if (_typeFilter is "" or "Desktop")
        {
            var desktopQuery = Db.DesktopSessions.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(_siteFilter))
                desktopQuery = desktopQuery.Where(s => s.SiteId == _siteFilter);

            var desktopSessions = await desktopQuery.OrderByDescending(s => s.IngestedAt).Take(50).ToListAsync();
            events.AddRange(desktopSessions.Select(s => new EventRow
            {
                Id = s.Id,
                SiteName = s.AppName,
                SourceType = "Desktop",
                IsFirstSeen = s.IsFirstInstall,
                Platform = s.Platform,
                DurationMs = s.DurationMs,
                IngestedAt = s.IngestedAt,
                Timestamp = s.SessionStart,
                SessionEnd = s.SessionEnd,
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

            var mobileSessions = await mobileQuery.OrderByDescending(s => s.IngestedAt).Take(50).ToListAsync();
            events.AddRange(mobileSessions.Select(s => new EventRow
            {
                Id = s.Id,
                SiteName = s.AppName,
                SourceType = "Mobile",
                IsFirstSeen = s.IsFirstInstall,
                Platform = s.Platform,
                DurationMs = s.DurationMs,
                IngestedAt = s.IngestedAt,
                Timestamp = s.SessionStart,
                SessionEnd = s.SessionEnd,
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

        _events = events.OrderByDescending(e => e.IngestedAt).Take(50).ToList();
    }

    private async Task ApplyFilters()
    {
        await LoadEvents();
    }

    private async Task OnHideBotsChanged(bool value)
    {
        _hideBots = value;
        await LoadEvents();
    }

    private static Color GetTypeColor(string type) => type switch
    {
        "Web" => Color.Info,
        "Desktop" => Color.Success,
        "Mobile" => Color.Warning,
        _ => Color.Default
    };

    private static Color GetWebEventColor(string? eventType) => eventType switch
    {
        "page_view" => Color.Info,
        "custom" => Color.Tertiary,
        "link_click" => Color.Warning,
        "circuit_close" => Color.Default,
        _ => Color.Default
    };

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

        // Web event fields
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
