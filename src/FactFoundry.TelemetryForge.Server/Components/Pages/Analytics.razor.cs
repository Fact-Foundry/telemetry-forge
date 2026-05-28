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

    private const int MaxSeries = 5;

    private string _period = "Last 7 Days";
    private string _selectedSiteId = "";
    private bool _loaded;
    private TimeZoneInfo _tz = TimeZoneInfo.Utc;

    private List<Site> _sites = [];
    private Dictionary<string, string?> _siteDomains = new();

    private ChartData _pageChart = new();
    private ChartData _countryChart = new();
    private ChartData _referrerChart = new();

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
        var dates = Enumerable.Range(0, (to.Date - from.Date).Days + 1)
            .Select(i => from.Date.AddDays(i))
            .ToList();

        var query = Db.WebEvents.AsNoTracking()
            .Where(e => e.IngestedAt >= from && e.IngestedAt < to.AddDays(1) && !e.IsBot && e.EventType == "page_view");

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
                SiteId = e.SiteId
            })
            .ToListAsync();

        var labels = dates.Select(FormatDateLabel).ToArray();

        _pageChart = BuildChart(events, dates, labels, e => string.IsNullOrEmpty(e.Page) ? "/" : e.Page);
        _countryChart = BuildChart(events, dates, labels, e => e.Country ?? "Unknown");
        _referrerChart = BuildReferrerChart(events, dates, labels);
    }

    private static ChartData BuildChart(
        List<EventProjection> events,
        List<DateTime> dates,
        string[] labels,
        Func<EventProjection, string> dimensionSelector)
    {
        var grouped = events
            .GroupBy(e => new { Dimension = dimensionSelector(e), Date = e.IngestedAt.Date })
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
            Name = Truncate(d.Dimension, 30),
            Data = dates.Select(date => (double)d.ByDate.GetValueOrDefault(date, 0)).ToArray()
        }).ToList();

        return new ChartData { Series = series, Labels = labels };
    }

    private ChartData BuildReferrerChart(
        List<EventProjection> events,
        List<DateTime> dates,
        string[] labels)
    {
        var withReferrer = events
            .Where(e => !string.IsNullOrEmpty(e.Referrer))
            .Select(e => new
            {
                e.SessionHash,
                e.IngestedAt,
                ReferrerDomain = ExtractDomain(e.Referrer!),
                SiteDomain = _siteDomains.GetValueOrDefault(e.SiteId)
            })
            .Where(e => e.ReferrerDomain != null && !IsSelfReferral(e.ReferrerDomain, e.SiteDomain))
            .ToList();

        var grouped = withReferrer
            .GroupBy(e => new { e.ReferrerDomain, Date = e.IngestedAt.Date })
            .GroupBy(g => g.Key.ReferrerDomain!)
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
            Name = Truncate(d.Dimension, 30),
            Data = dates.Select(date => (double)d.ByDate.GetValueOrDefault(date, 0)).ToArray()
        }).ToList();

        return new ChartData { Series = series, Labels = labels };
    }

    private (DateTime from, DateTime to) GetDateRange()
    {
        var now = DateTime.UtcNow;
        return _period switch
        {
            "Today" => (now.Date, now.Date),
            "Last 7 Days" => (now.Date.AddDays(-6), now.Date),
            "Last 30 Days" => (now.Date.AddDays(-29), now.Date),
            _ => (now.Date.AddDays(-6), now.Date)
        };
    }

    private string FormatDateLabel(DateTime date) =>
        TimeZoneInfo.ConvertTimeFromUtc(date, _tz).ToString("M/d");

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

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";

    private class EventProjection
    {
        public DateTime IngestedAt { get; set; }
        public string SessionHash { get; set; } = string.Empty;
        public string Page { get; set; } = string.Empty;
        public string? Country { get; set; }
        public string? Referrer { get; set; }
        public string SiteId { get; set; } = string.Empty;
    }

    private class ChartData
    {
        public List<ChartSeries<double>> Series { get; set; } = [];
        public string[] Labels { get; set; } = [];
    }
}
