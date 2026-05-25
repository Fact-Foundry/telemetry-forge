using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace FactFoundry.TelemetryForge.Server.Components.Pages;

/// <summary>
/// Event Stream page showing a filterable feed of recent enriched events with expandable detail.
/// </summary>
public partial class EventStream : ComponentBase
{
    [Inject] private TelemetryForgeDbContext Db { get; set; } = default!;

    private List<Site> _sites = [];
    private List<EventRow> _events = [];
    private string _siteFilter = string.Empty;
    private string _typeFilter = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        _sites = await Db.Sites.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        await LoadEvents();
    }

    private async Task LoadEvents()
    {
        var webQuery = Db.WebSessions.AsNoTracking().AsQueryable();
        var desktopQuery = Db.DesktopSessions.AsNoTracking().AsQueryable();
        var mobileQuery = Db.MobileSessions.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(_siteFilter))
        {
            webQuery = webQuery.Where(s => s.SiteId == _siteFilter);
            desktopQuery = desktopQuery.Where(s => s.SiteId == _siteFilter);
            mobileQuery = mobileQuery.Where(s => s.SiteId == _siteFilter);
        }

        var events = new List<EventRow>();

        if (_typeFilter is "" or "Web")
        {
            var webSessions = await webQuery.OrderByDescending(s => s.IngestedAt).Take(50).ToListAsync();
            events.AddRange(webSessions.Select(s => new EventRow
            {
                Id = s.Id,
                SiteName = s.SiteName,
                EventType = "Web",
                IsFirstSeen = s.IsFirstVisit,
                Platform = s.Platform,
                DurationMs = s.DurationMs,
                IngestedAt = s.IngestedAt,
                SessionStart = s.SessionStart,
                SessionEnd = s.SessionEnd,
                EntryPage = s.EntryPage,
                ExitPage = s.ExitPage,
                PagePath = s.PagePath,
                PageCount = s.PageCount,
                StatusCodes = s.StatusCodes,
                Language = s.Language,
                Referrer = s.Referrer,
                Country = s.Country,
                Region = s.Region,
                Browser = s.Browser,
                Os = s.Os,
                DeviceType = s.DeviceType,
                SessionHash = s.SessionHash
            }));
        }

        if (_typeFilter is "" or "Desktop")
        {
            var desktopSessions = await desktopQuery.OrderByDescending(s => s.IngestedAt).Take(50).ToListAsync();
            events.AddRange(desktopSessions.Select(s => new EventRow
            {
                Id = s.Id,
                SiteName = s.AppName,
                EventType = "Desktop",
                IsFirstSeen = s.IsFirstInstall,
                Platform = s.Platform,
                DurationMs = s.DurationMs,
                IngestedAt = s.IngestedAt,
                SessionStart = s.SessionStart,
                SessionEnd = s.SessionEnd,
                AppVersion = s.AppVersion,
                OsVersion = s.OsVersion,
                LicenseTier = s.LicenseTier,
                FeaturePath = s.FeaturePath,
                FeatureCount = s.FeatureCount,
                ErrorEvents = s.ErrorEvents,
                ErrorCount = s.ErrorCount,
                FingerprintHash = s.FingerprintHash
            }));
        }

        if (_typeFilter is "" or "Mobile")
        {
            var mobileSessions = await mobileQuery.OrderByDescending(s => s.IngestedAt).Take(50).ToListAsync();
            events.AddRange(mobileSessions.Select(s => new EventRow
            {
                Id = s.Id,
                SiteName = s.AppName,
                EventType = "Mobile",
                IsFirstSeen = s.IsFirstInstall,
                Platform = s.Platform,
                DurationMs = s.DurationMs,
                IngestedAt = s.IngestedAt,
                SessionStart = s.SessionStart,
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

    private static Color GetTypeColor(string type) => type switch
    {
        "Web" => Color.Info,
        "Desktop" => Color.Success,
        "Mobile" => Color.Warning,
        _ => Color.Default
    };

    private static string FormatDuration(int ms) => ms switch
    {
        < 1000 => $"{ms}ms",
        < 60000 => $"{ms / 1000.0:F1}s",
        _ => $"{ms / 60000.0:F1}m"
    };

    private class EventRow
    {
        public long Id { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public bool IsFirstSeen { get; set; }
        public string Platform { get; set; } = string.Empty;
        public int DurationMs { get; set; }
        public DateTime IngestedAt { get; set; }
        public DateTime SessionStart { get; set; }
        public DateTime SessionEnd { get; set; }
        public bool Expanded { get; set; }

        // Web fields
        public string? EntryPage { get; set; }
        public string? ExitPage { get; set; }
        public List<string> PagePath { get; set; } = [];
        public int PageCount { get; set; }
        public Dictionary<string, int> StatusCodes { get; set; } = [];
        public string? Language { get; set; }
        public string? Referrer { get; set; }
        public string? Country { get; set; }
        public string? Region { get; set; }
        public string? Browser { get; set; }
        public string? Os { get; set; }
        public string? DeviceType { get; set; }
        public string? SessionHash { get; set; }

        // Desktop/Mobile fields
        public string? AppVersion { get; set; }
        public string? OsVersion { get; set; }
        public string? LicenseTier { get; set; }
        public string? FingerprintHash { get; set; }
        public string? DeviceHash { get; set; }
        public string? DeviceHashType { get; set; }
        public List<string> FeaturePath { get; set; } = [];
        public int FeatureCount { get; set; }
        public List<StoredErrorEvent> ErrorEvents { get; set; } = [];
        public int ErrorCount { get; set; }
    }
}
