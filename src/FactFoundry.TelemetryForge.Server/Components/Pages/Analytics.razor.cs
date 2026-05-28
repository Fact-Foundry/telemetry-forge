using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using FactFoundry.TelemetryForge.Server.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace FactFoundry.TelemetryForge.Server.Components.Pages;

/// <summary>
/// Analytics page with line charts for sessions by page, country, and referrer.
/// </summary>
public partial class Analytics : ComponentBase
{
    [Inject] private TelemetryForgeDbContext Db { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;

    private const int MaxSeries = 10;

    private string _period = "Last 7 Days";
    private string _selectedSiteId = "";
    private bool _loaded;
    private TimeZoneInfo _tz = TimeZoneInfo.Utc;

    private List<Site> _sites = [];
    private Dictionary<string, string?> _siteDomains = new();

    private ChartData _pageChart = new();
    private ChartData _countryChart = new();
    private ChartData _durationChart = new();
    private ChartData _referrerChart = new();

    private PieData _browserPie = new();
    private PieData _osPie = new();
    private PieData _devicePie = new();

    private readonly LineChartOptions _lineOptions = new() { YAxisTicks = 10 };

    protected override async Task OnInitializedAsync()
    {
        var tzId = await AuthService.GetServerSettingAsync("Display:Timezone");
        if (!string.IsNullOrEmpty(tzId))
        {
            try { _tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
            catch (TimeZoneNotFoundException) { }
        }

        _sites = await Db.Sites.AsNoTracking().Where(s => s.Type == SiteType.Web).OrderBy(s => s.Name).ToListAsync();
        _siteDomains = _sites.ToDictionary(s => s.Id, s => s.Domain);
        await LoadCharts();
        _loaded = true;
    }

    private async Task OnPeriodChanged(string period)
    {
        _period = period;
        await LoadCharts();
    }

    private async Task OnSiteChanged(string siteId)
    {
        _selectedSiteId = siteId;
        await LoadCharts();
    }

    private async Task LoadCharts()
    {
        var (from, to) = GetDateRange();
        var localFrom = TimeZoneInfo.ConvertTimeFromUtc(from, _tz).Date;
        var localTo = TimeZoneInfo.ConvertTimeFromUtc(to, _tz).Date;
        var queryEnd = TimeZoneInfo.ConvertTimeToUtc(localTo.AddDays(1), _tz);
        var dates = Enumerable.Range(0, (localTo - localFrom).Days + 1)
            .Select(i => localFrom.AddDays(i))
            .ToList();

        var query = Db.WebEvents.AsNoTracking()
            .Where(e => e.IngestedAt >= from && e.IngestedAt < queryEnd && !e.IsBot && e.EventType == "page_view");

        if (!string.IsNullOrEmpty(_selectedSiteId))
            query = query.Where(e => e.SiteId == _selectedSiteId);

        var events = await query
            .Select(e => new EventProjection
            {
                IngestedAt = e.IngestedAt,
                SessionHash = e.SessionHash,
                Page = e.Page,
                Country = e.Country,
                Referrer = e.Referrer,
                SiteId = e.SiteId,
                Browser = e.Browser,
                Os = e.Os,
                DeviceType = e.DeviceType
            })
            .ToListAsync();

        foreach (var e in events)
            e.LocalDate = TimeZoneInfo.ConvertTimeFromUtc(e.IngestedAt, _tz).Date;

        var labels = dates.Select(d => d.ToString("M/d")).ToArray();

        _pageChart = BuildChart(events, dates, labels, e => string.IsNullOrEmpty(e.Page) ? "/" : e.Page);
        _countryChart = BuildChart(events, dates, labels, e => e.Country ?? "Unknown");
        _durationChart = await BuildDurationChartAsync(from, queryEnd, dates, labels);
        _referrerChart = BuildReferrerChart(events, dates, labels);

        _browserPie = BuildPie(events, e => e.Browser ?? "Unknown");
        _osPie = BuildPie(events, e => e.Os ?? "Unknown");
        _devicePie = BuildPie(events, e => e.DeviceType ?? "Unknown");
    }

    private static ChartData BuildChart(
        List<EventProjection> events,
        List<DateTime> dates,
        string[] labels,
        Func<EventProjection, string> dimensionSelector)
    {
        var grouped = events
            .GroupBy(e => new { Dimension = dimensionSelector(e), Date = e.LocalDate })
            .GroupBy(g => g.Key.Dimension)
            .Select(g => new
            {
                Dimension = g.Key,
                Total = g.Sum(d => d.Select(e => e.SessionHash).Distinct().Count()),
                ByDate = g.ToDictionary(d => d.Key.Date, d => d.Select(e => e.SessionHash).Distinct().Count())
            })
            .OrderByDescending(g => g.Total)
            .Take(MaxSeries)
            .ToList();

        var series = grouped.Select(d => new ChartSeries<double>
        {
            Name = Truncate(d.Dimension),
            Data = dates.Select(date => (double)d.ByDate.GetValueOrDefault(date, 0)).ToArray()
        }).ToList();

        return new ChartData { Series = series, Labels = labels };
    }

    private async Task<ChartData> BuildDurationChartAsync(
        DateTime from, DateTime queryEnd, List<DateTime> dates, string[] labels)
    {
        var query = Db.WebEvents.AsNoTracking()
            .Where(e => e.IngestedAt >= from && e.IngestedAt < queryEnd && !e.IsBot);

        if (!string.IsNullOrEmpty(_selectedSiteId))
            query = query.Where(e => e.SiteId == _selectedSiteId);

        var rawEvents = await query
            .Select(e => new { e.SessionHash, e.Page, e.EventType, e.Timestamp, e.IngestedAt })
            .ToListAsync();

        var sessionDurations = rawEvents
            .GroupBy(e => e.SessionHash)
            .ToDictionary(
                g => g.Key,
                g => (g.Max(e => e.Timestamp) - g.Min(e => e.Timestamp)).TotalSeconds);

        var pageSessionData = rawEvents
            .Where(e => e.EventType == "page_view")
            .Select(e => new
            {
                Page = string.IsNullOrEmpty(e.Page) ? "/" : e.Page,
                LocalDate = TimeZoneInfo.ConvertTimeFromUtc(e.IngestedAt, _tz).Date,
                e.SessionHash,
                Duration = sessionDurations.GetValueOrDefault(e.SessionHash, 0)
            })
            .Where(x => x.Duration > 0)
            .GroupBy(x => new { x.Page, x.SessionHash })
            .Select(g => g.First())
            .ToList();

        var grouped = pageSessionData
            .GroupBy(x => new { x.Page, x.LocalDate })
            .GroupBy(g => g.Key.Page)
            .Select(g => new
            {
                Page = g.Key,
                Total = g.Sum(d => d.Count()),
                ByDate = g.ToDictionary(d => d.Key.LocalDate, d => Math.Round(d.Average(x => x.Duration), 1))
            })
            .OrderByDescending(g => g.Total)
            .Take(MaxSeries)
            .ToList();

        var series = grouped.Select(d => new ChartSeries<double>
        {
            Name = Truncate(d.Page),
            Data = dates.Select(date => d.ByDate.GetValueOrDefault(date, 0)).ToArray()
        }).ToList();

        return new ChartData { Series = series, Labels = labels };
    }

    private ChartData BuildReferrerChart(
        List<EventProjection> events,
        List<DateTime> dates,
        string[] labels)
    {
        var classified = events
            .Select(e =>
            {
                if (string.IsNullOrEmpty(e.Referrer))
                    return new { e.SessionHash, e.LocalDate, ReferrerDomain = "Direct", IsSelf = false };

                var domain = ExtractDomain(e.Referrer);
                var siteDomain = _siteDomains.GetValueOrDefault(e.SiteId);
                var isSelf = domain != null && IsSelfReferral(domain, siteDomain);
                return new { e.SessionHash, e.LocalDate, ReferrerDomain = domain ?? "Direct", IsSelf = isSelf };
            })
            .Where(e => !e.IsSelf)
            .ToList();

        var grouped = classified
            .GroupBy(e => new { e.ReferrerDomain, Date = e.LocalDate })
            .GroupBy(g => g.Key.ReferrerDomain)
            .Select(g => new
            {
                Dimension = g.Key,
                Total = g.Sum(d => d.Select(e => e.SessionHash).Distinct().Count()),
                ByDate = g.ToDictionary(d => d.Key.Date, d => d.Select(e => e.SessionHash).Distinct().Count())
            })
            .OrderByDescending(g => g.Total)
            .Take(MaxSeries)
            .ToList();

        var series = grouped.Select(d => new ChartSeries<double>
        {
            Name = Truncate(d.Dimension),
            Data = dates.Select(date => (double)d.ByDate.GetValueOrDefault(date, 0)).ToArray()
        }).ToList();

        return new ChartData { Series = series, Labels = labels };
    }

    private static PieData BuildPie(
        List<EventProjection> events,
        Func<EventProjection, string> dimensionSelector)
    {
        var grouped = events
            .GroupBy(e => new { Dimension = dimensionSelector(e), e.SessionHash })
            .Select(g => g.Key)
            .GroupBy(x => x.Dimension)
            .Select(g => new { Dimension = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(MaxSeries)
            .ToList();

        return new PieData
        {
            Series = [new ChartSeries<double>
            {
                Data = grouped.Select(g => (double)g.Count).ToArray()
            }],
            Labels = grouped.Select(g => $"{g.Dimension} ({g.Count})").ToArray()
        };
    }

    private (DateTime from, DateTime to) GetDateRange()
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz);
        var (localFrom, localTo) = _period switch
        {
            "Today" => (nowLocal.Date, nowLocal.Date),
            "Last 7 Days" => (nowLocal.Date.AddDays(-6), nowLocal.Date),
            "Last 30 Days" => (nowLocal.Date.AddDays(-29), nowLocal.Date),
            _ => (nowLocal.Date.AddDays(-6), nowLocal.Date)
        };
        return (TimeZoneInfo.ConvertTimeToUtc(localFrom, _tz), TimeZoneInfo.ConvertTimeToUtc(localTo, _tz));
    }

    private static string? ExtractDomain(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return uri.Host.ToLowerInvariant();
        return null;
    }

    private static bool IsSelfReferral(string? referrerDomain, string? siteDomain)
    {
        if (string.IsNullOrEmpty(referrerDomain) || string.IsNullOrEmpty(siteDomain))
            return false;
        return referrerDomain.Equals(siteDomain, StringComparison.OrdinalIgnoreCase)
               || referrerDomain.EndsWith("." + siteDomain, StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string value, int maxLength = 60) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";

    private class EventProjection
    {
        public DateTime IngestedAt { get; set; }
        public DateTime LocalDate { get; set; }
        public string SessionHash { get; set; } = string.Empty;
        public string Page { get; set; } = string.Empty;
        public string? Country { get; set; }
        public string? Referrer { get; set; }
        public string SiteId { get; set; } = string.Empty;
        public string? Browser { get; set; }
        public string? Os { get; set; }
        public string? DeviceType { get; set; }
    }

    private class ChartData
    {
        public List<ChartSeries<double>> Series { get; set; } = [];
        public string[] Labels { get; set; } = [];
    }

    private class PieData
    {
        public List<ChartSeries<double>> Series { get; set; } = [];
        public string[] Labels { get; set; } = [];
    }
}
