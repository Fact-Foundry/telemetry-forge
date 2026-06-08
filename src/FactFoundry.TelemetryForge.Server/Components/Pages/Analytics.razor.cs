using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using FactFoundry.TelemetryForge.Server.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using FactFoundry.Blazor.Charts.Models;
using OoxSpreadsheet;
using OslSpreadsheet.Models;

namespace FactFoundry.TelemetryForge.Server.Components.Pages;

/// <summary>
/// Analytics page with line charts for sessions by page, country, and referrer.
/// </summary>
public partial class Analytics : ComponentBase
{
    [Inject] private TelemetryForgeDbContext Db { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    private const int MaxSeries = 10;

    private string _period = "Last 7 Days";
    private string _selectedSiteId = "";
    private string _selectedOs = "";
    private string _selectedPage = "";
    private bool _loaded;
    private TimeZoneInfo _tz = TimeZoneInfo.Utc;

    private List<Site> _sites = [];
    private List<string> _osOptions = [];
    private List<string> _pageOptions = [];
    private Dictionary<string, string?> _siteDomains = new();

    private ChartData _pageChart = new();
    private List<MapDataPoint> _countryMap = [];
    private ChartData _durationChart = new();
    private ChartData _referrerChart = new();

    private PieData _browserPie = new();
    private PieData _osPie = new();
    private PieData _devicePie = new();

    // Page Breakdown tab
    private string _breakdownPeriod = "Past Week";
    private string _breakdownSiteId = "";
    private bool _breakdownLoaded;
    private bool _exporting;
    private List<DateTime> _breakdownDates = [];
    private List<PageBreakdownRow> _breakdownRows = [];

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

    private async Task OnOsChanged(string os)
    {
        _selectedOs = os;
        await LoadCharts();
    }

    private async Task OnPageChanged(string page)
    {
        _selectedPage = page;
        await LoadCharts();
    }

    private async Task OnTabActivated(int index)
    {
        // Lazily load the Page Breakdown tab (index 1) the first time it is opened.
        if (index == 1 && !_breakdownLoaded)
            await LoadBreakdown();
    }

    private async Task OnBreakdownPeriodChanged(string period)
    {
        _breakdownPeriod = period;
        await LoadBreakdown();
    }

    private async Task OnBreakdownSiteChanged(string siteId)
    {
        _breakdownSiteId = siteId;
        await LoadBreakdown();
    }

    private async Task LoadBreakdown()
    {
        var (from, to, dates) = GetBreakdownRange();
        _breakdownDates = dates;

        var query = Db.WebEvents.AsNoTracking()
            .Where(e => e.IngestedAt >= from && e.IngestedAt < to && !e.IsBot && !e.IsIgnored && e.EventType == "page_view");

        if (!string.IsNullOrEmpty(_breakdownSiteId))
            query = query.Where(e => e.SiteId == _breakdownSiteId);

        var raw = await query
            .Select(e => new { e.Page, e.Os, e.Browser, e.IngestedAt })
            .ToListAsync();

        var events = raw.Select(e => new PageBreakdownEvent(
            e.Page, e.Os, e.Browser,
            TimeZoneInfo.ConvertTimeFromUtc(e.IngestedAt, _tz).Date)).ToList();

        _breakdownRows = PageBreakdownBuilder.Build(events, _breakdownDates).ToList();
        _breakdownLoaded = true;
    }

    private (DateTime from, DateTime to, List<DateTime> dates) GetBreakdownRange()
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz);
        var days = _breakdownPeriod == "Past Month" ? 30 : 7;
        var localFrom = nowLocal.Date.AddDays(-(days - 1));
        var localTo = nowLocal.Date;

        var dates = Enumerable.Range(0, (localTo - localFrom).Days + 1)
            .Select(i => localFrom.AddDays(i))
            .ToList();

        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(localFrom, _tz);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(localTo.AddDays(1), _tz);
        return (fromUtc, toUtc, dates);
    }

    private async Task ExportBreakdownXlsx()
    {
        _exporting = true;
        try
        {
            await using var spreadsheet = new Spreadsheet();
            var sheet = spreadsheet.Workbook.AddSheet("Page Breakdown");

            // Header row (1-based rows/columns).
            sheet.AddCell(1, 1, "Page");
            sheet.AddCell(1, 2, "OS");
            sheet.AddCell(1, 3, "Browser");
            var col = 4;
            foreach (var d in _breakdownDates)
                sheet.AddCell(1, col++, d.ToString("yyyy-MM-dd"));
            sheet.AddCell(1, col, "Total");

            // Data rows.
            var rowIndex = 2;
            foreach (var r in _breakdownRows)
            {
                sheet.AddCell(rowIndex, 1, r.Page);
                sheet.AddCell(rowIndex, 2, r.Os);
                sheet.AddCell(rowIndex, 3, r.Browser);
                col = 4;
                foreach (var d in _breakdownDates)
                    sheet.AddCell(rowIndex, col++, r.Counts.GetValueOrDefault(d, 0).ToString(), CellValueType.Int64);
                sheet.AddCell(rowIndex, col, r.Total.ToString(), CellValueType.Int64);
                rowIndex++;
            }

            var bytes = await spreadsheet.GenerateXlsxFileAsync();
            var base64 = Convert.ToBase64String(bytes);
            var fileName = $"page-breakdown-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
            const string mime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            await Js.InvokeVoidAsync("eval",
                $"(() => {{ const a = document.createElement('a'); a.href = 'data:{mime};base64,{base64}'; a.download = '{fileName}'; a.click(); }})()");
        }
        finally
        {
            _exporting = false;
        }
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
            .Where(e => e.IngestedAt >= from && e.IngestedAt < queryEnd && !e.IsBot && !e.IsIgnored && e.EventType == "page_view");

        if (!string.IsNullOrEmpty(_selectedSiteId))
            query = query.Where(e => e.SiteId == _selectedSiteId);

        var allEvents = await query
            .Select(e => new EventProjection
            {
                IngestedAt = e.IngestedAt,
                SessionHash = e.SessionHash,
                Page = e.Page,
                Country = e.Country,
                CountryCode = e.CountryCode,
                Referrer = e.Referrer,
                SiteId = e.SiteId,
                Browser = e.Browser,
                Os = e.Os,
                DeviceType = e.DeviceType
            })
            .ToListAsync();

        _osOptions = allEvents
            .Select(e => e.Os ?? "Unknown")
            .Distinct()
            .OrderBy(o => o)
            .ToList();

        // Distinct pages for the Page filter, scoped by the active site/period (not narrowed by the
        // OS or page selections themselves). Reset the selection if it's no longer available.
        _pageOptions = allEvents
            .Select(e => string.IsNullOrEmpty(e.Page) ? "/" : e.Page)
            .Distinct()
            .OrderBy(p => p)
            .ToList();
        if (!string.IsNullOrEmpty(_selectedPage) && !_pageOptions.Contains(_selectedPage))
            _selectedPage = "";

        var events = allEvents;
        if (!string.IsNullOrEmpty(_selectedOs))
            events = events.Where(e => (e.Os ?? "Unknown") == _selectedOs).ToList();
        if (!string.IsNullOrEmpty(_selectedPage))
            events = events.Where(e => (string.IsNullOrEmpty(e.Page) ? "/" : e.Page) == _selectedPage).ToList();

        foreach (var e in events)
            e.LocalDate = TimeZoneInfo.ConvertTimeFromUtc(e.IngestedAt, _tz).Date;

        var labels = dates.Select(d => d.ToString("M/d")).ToList();

        _pageChart = BuildChart(events, dates, labels, e => string.IsNullOrEmpty(e.Page) ? "/" : e.Page);
        _countryMap = BuildCountryMap(events);
        _durationChart = await BuildDurationChartAsync(from, queryEnd, dates, labels);
        _referrerChart = BuildReferrerChart(events, dates, labels);

        _browserPie = BuildPie(events, e => e.Browser ?? "Unknown");
        _osPie = BuildPie(events, e => e.Os ?? "Unknown");
        _devicePie = BuildPie(events, e => e.DeviceType ?? "Unknown");
    }

    private static ChartData BuildChart(
        List<EventProjection> events,
        List<DateTime> dates,
        List<string> labels,
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

        var series = grouped.Select(d => new ChartSeries
        {
            Label = Truncate(d.Dimension),
            Values = dates.Select(date => (decimal)d.ByDate.GetValueOrDefault(date, 0)).ToList()
        }).ToList();

        return new ChartData { Series = series, Labels = labels };
    }

    /// <summary>
    /// Aggregates unique sessions per country (ISO alpha-2 code) for the world map heatmap.
    /// </summary>
    private static List<MapDataPoint> BuildCountryMap(List<EventProjection> events)
    {
        return events
            .Where(e => !string.IsNullOrEmpty(e.CountryCode))
            .GroupBy(e => new { e.CountryCode, e.SessionHash })
            .Select(g => g.Key)
            .GroupBy(x => x.CountryCode!)
            .Select(g => new MapDataPoint { CountryCode = g.Key, Value = g.Count() })
            .ToList();
    }

    private async Task<ChartData> BuildDurationChartAsync(
        DateTime from, DateTime queryEnd, List<DateTime> dates, List<string> labels)
    {
        var query = Db.WebEvents.AsNoTracking()
            .Where(e => e.IngestedAt >= from && e.IngestedAt < queryEnd && !e.IsBot && !e.IsIgnored);

        if (!string.IsNullOrEmpty(_selectedSiteId))
            query = query.Where(e => e.SiteId == _selectedSiteId);

        if (!string.IsNullOrEmpty(_selectedOs))
            query = query.Where(e => e.Os == _selectedOs);

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
                ByDate = g.ToDictionary(d => d.Key.LocalDate, d => Math.Round(d.Average(x => x.Duration) / 60.0, 1))
            })
            .OrderByDescending(g => g.Total)
            .Take(MaxSeries)
            .ToList();

        var series = grouped.Select(d => new ChartSeries
        {
            Label = Truncate(d.Page),
            Values = dates.Select(date => (decimal)d.ByDate.GetValueOrDefault(date, 0)).ToList()
        }).ToList();

        return new ChartData { Series = series, Labels = labels };
    }

    private ChartData BuildReferrerChart(
        List<EventProjection> events,
        List<DateTime> dates,
        List<string> labels)
    {
        var classified = events
            .Select(e =>
            {
                if (string.IsNullOrEmpty(e.Referrer))
                    return new { e.SessionHash, e.LocalDate, ReferrerDomain = "Unknown" };

                var domain = ExtractDomain(e.Referrer);
                var siteDomain = _siteDomains.GetValueOrDefault(e.SiteId);
                var isSelf = domain != null && IsSelfReferral(domain, siteDomain);
                if (isSelf)
                    return new { e.SessionHash, e.LocalDate, ReferrerDomain = "Direct" };

                return new { e.SessionHash, e.LocalDate, ReferrerDomain = domain ?? "Unknown" };
            })
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

        var series = grouped.Select(d => new ChartSeries
        {
            Label = Truncate(d.Dimension),
            Values = dates.Select(date => (decimal)d.ByDate.GetValueOrDefault(date, 0)).ToList()
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
            Data = grouped.Select(g => new ChartSegment
            {
                Label = g.Dimension,
                Value = g.Count
            }).ToList()
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
        public string? CountryCode { get; set; }
        public string? Referrer { get; set; }
        public string SiteId { get; set; } = string.Empty;
        public string? Browser { get; set; }
        public string? Os { get; set; }
        public string? DeviceType { get; set; }
    }

    private class ChartData
    {
        public List<ChartSeries> Series { get; set; } = [];
        public List<string> Labels { get; set; } = [];
    }

    private class PieData
    {
        public List<ChartSegment> Data { get; set; } = [];
    }
}
