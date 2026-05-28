using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using FactFoundry.TelemetryForge.Server.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace FactFoundry.TelemetryForge.Server.Components.Pages;

/// <summary>
/// Security page showing bot traffic and retroactive bot scanning.
/// </summary>
public partial class Security : ComponentBase
{
    [Inject] private TelemetryForgeDbContext Db { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private BotDetectionService BotDetection { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private TimeZoneInfo _tz = TimeZoneInfo.Utc;
    private List<Site> _sites = [];
    private List<BotEventRow> _events = [];
    private string _siteFilter = string.Empty;
    private string _reasonFilter = string.Empty;
    private bool _scanning;

    private int _botsToday;
    private int _botsThisWeek;
    private int _botsThisMonth;
    private int _botsTotal;
    private Dictionary<string, int> _reasonCounts = new();
    private PieData _botBrowserPie = new();

    protected override async Task OnInitializedAsync()
    {
        var tzId = await AuthService.GetServerSettingAsync("Display:Timezone");
        if (!string.IsNullOrEmpty(tzId))
        {
            try { _tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
            catch (TimeZoneNotFoundException) { }
        }

        _sites = await Db.Sites.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        await LoadStats();
        await LoadEvents();
    }

    private async Task LoadStats()
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz);
        var todayStart = TimeZoneInfo.ConvertTimeToUtc(nowLocal.Date, _tz);
        var weekStart = TimeZoneInfo.ConvertTimeToUtc(nowLocal.Date.AddDays(-(int)nowLocal.DayOfWeek), _tz);
        var monthStart = TimeZoneInfo.ConvertTimeToUtc(new DateTime(nowLocal.Year, nowLocal.Month, 1), _tz);

        var query = Db.WebEvents.AsNoTracking().Where(e => e.IsBot);

        if (!string.IsNullOrEmpty(_siteFilter))
            query = query.Where(e => e.SiteId == _siteFilter);

        if (!string.IsNullOrEmpty(_reasonFilter))
            query = query.Where(e => e.BotReason == _reasonFilter);

        var botEvents = await query
            .Select(e => new { e.IngestedAt, e.BotReason, e.Browser, e.SessionHash })
            .ToListAsync();

        _botsTotal = botEvents.Count;
        _botsToday = botEvents.Count(e => e.IngestedAt >= todayStart);
        _botsThisWeek = botEvents.Count(e => e.IngestedAt >= weekStart);
        _botsThisMonth = botEvents.Count(e => e.IngestedAt >= monthStart);

        _reasonCounts = botEvents
            .GroupBy(e => e.BotReason ?? "unknown")
            .ToDictionary(g => g.Key, g => g.Count())
            .OrderByDescending(kvp => kvp.Value)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        var botBrowserCounts = botEvents
            .GroupBy(e => new { Browser = e.Browser ?? "Unknown", e.SessionHash })
            .Select(g => g.Key)
            .GroupBy(x => x.Browser)
            .Select(g => new { Browser = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        _botBrowserPie = new PieData
        {
            Series = [new ChartSeries<double>
            {
                Data = botBrowserCounts.Select(x => (double)x.Count).ToArray()
            }],
            Labels = botBrowserCounts.Select(x => $"{x.Browser} ({x.Count})").ToArray()
        };
    }

    private async Task LoadEvents()
    {
        var query = Db.WebEvents.AsNoTracking().Where(e => e.IsBot);

        if (!string.IsNullOrEmpty(_siteFilter))
            query = query.Where(e => e.SiteId == _siteFilter);

        if (!string.IsNullOrEmpty(_reasonFilter))
            query = query.Where(e => e.BotReason == _reasonFilter);

        var events = await query
            .OrderByDescending(e => e.Timestamp)
            .Take(100)
            .ToListAsync();

        _events = events.Select(e => new BotEventRow
        {
            Id = e.Id,
            SiteName = e.SiteName,
            EventType = e.EventType,
            BotReason = e.BotReason,
            Page = e.Page,
            SessionHash = e.SessionHash,
            Country = e.Country,
            Browser = e.Browser,
            Os = e.Os,
            DeviceType = e.DeviceType,
            Language = e.Language,
            Referrer = e.Referrer,
            StatusCode = e.StatusCode,
            Timestamp = e.Timestamp.UtcDateTime
        }).ToList();
    }

    private async Task ApplyFilters()
    {
        await LoadStats();
        await LoadEvents();
    }

    private async Task RunScan()
    {
        _scanning = true;
        StateHasChanged();

        try
        {
            var result = await BotDetection.ScanAsync();
            await LoadStats();
            await LoadEvents();

            if (result.NewlyFlagged == 0 && result.ReasonsBackfilled == 0)
            {
                Snackbar.Add("Scan complete — no new bots found.", Severity.Info);
            }
            else
            {
                var parts = new List<string>();
                if (result.NewlyFlagged > 0)
                    parts.Add($"{result.NewlyFlagged} events flagged as bot");
                if (result.ReasonsBackfilled > 0)
                    parts.Add($"{result.ReasonsBackfilled} reasons backfilled");
                Snackbar.Add($"Scan complete — {string.Join(", ", parts)}.", Severity.Success);
            }
        }
        catch (Exception)
        {
            Snackbar.Add("Scan failed. Check server logs.", Severity.Error);
        }
        finally
        {
            _scanning = false;
        }
    }

    private string FormatTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc, _tz).ToString("M/d/yyyy H:mm");

    private static Color GetReasonColor(string? reason) => reason switch
    {
        "user-agent" => Color.Error,
        "no-language" => Color.Warning,
        "country-hop" => Color.Tertiary,
        "page-velocity" => Color.Info,
        "path-scan" => Color.Dark,
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

    private class BotEventRow
    {
        public long Id { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string? BotReason { get; set; }
        public string Page { get; set; } = string.Empty;
        public string SessionHash { get; set; } = string.Empty;
        public string? Country { get; set; }
        public string? Browser { get; set; }
        public string? Os { get; set; }
        public string? DeviceType { get; set; }
        public string? Language { get; set; }
        public string? Referrer { get; set; }
        public int StatusCode { get; set; }
        public DateTime Timestamp { get; set; }
        public bool Expanded { get; set; }
    }

    private class PieData
    {
        public List<ChartSeries<double>> Series { get; set; } = [];
        public string[] Labels { get; set; } = [];
    }
}
