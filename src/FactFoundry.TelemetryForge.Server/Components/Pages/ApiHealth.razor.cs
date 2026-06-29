using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using FactFoundry.TelemetryForge.Server.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using FactFoundry.Blazor.Charts.Models;

namespace FactFoundry.TelemetryForge.Server.Components.Pages;

/// <summary>
/// Health-centric dashboard for API telemetry: request volume, status-code split,
/// error rate, latency, and top endpoints.
/// </summary>
public partial class ApiHealth : ComponentBase
{
    [Inject] private TelemetryForgeDbContext Db { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;

    private const int MaxEndpoints = 15;

    private string _period = "Last 7 Days";
    private string _selectedSiteId = "";
    private bool _loaded;
    private TimeZoneInfo _tz = TimeZoneInfo.Utc;

    private List<Site> _sites = [];

    private int _totalRequests;
    private double _errorRatePct;
    private int _avgLatencyMs;
    private int _p95LatencyMs;

    private ChartData _volumeChart = new();
    private PieData _statusPie = new();
    private List<EndpointRow> _endpointRows = [];

    /// <summary>
    /// Loads the configured display timezone and the list of API-type sites, then renders the dashboard.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        var tzId = await AuthService.GetServerSettingAsync("Display:Timezone");
        if (!string.IsNullOrEmpty(tzId))
        {
            try { _tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
            catch (TimeZoneNotFoundException) { }
        }

        _sites = await Db.Sites.AsNoTracking()
            .Where(s => s.Type == SiteType.Api)
            .OrderBy(s => s.Name)
            .ToListAsync();

        await Load();
        _loaded = true;
    }

    private async Task OnPeriodChanged(string period)
    {
        _period = period;
        await Load();
    }

    private async Task OnSiteChanged(string siteId)
    {
        _selectedSiteId = siteId;
        await Load();
    }

    private async Task Load()
    {
        var (from, to) = GetDateRange();
        var localFrom = TimeZoneInfo.ConvertTimeFromUtc(from, _tz).Date;
        var localTo = TimeZoneInfo.ConvertTimeFromUtc(to, _tz).Date;
        var queryEnd = TimeZoneInfo.ConvertTimeToUtc(localTo.AddDays(1), _tz);
        var dates = Enumerable.Range(0, (localTo - localFrom).Days + 1)
            .Select(i => localFrom.AddDays(i))
            .ToList();

        var query = Db.ApiEvents.AsNoTracking()
            .Where(e => e.IngestedAt >= from && e.IngestedAt < queryEnd);

        if (!string.IsNullOrEmpty(_selectedSiteId))
            query = query.Where(e => e.SiteId == _selectedSiteId);

        var events = await query
            .Select(e => new EventProjection
            {
                IngestedAt = e.IngestedAt,
                RouteTemplate = e.RouteTemplate,
                Method = e.Method,
                StatusCode = e.StatusCode,
                LatencyMs = e.LatencyMs
            })
            .ToListAsync();

        foreach (var e in events)
            e.LocalDate = TimeZoneInfo.ConvertTimeFromUtc(e.IngestedAt, _tz).Date;

        BuildSummary(events);

        var labels = dates.Select(d => d.ToString("M/d")).ToList();
        _volumeChart = BuildVolumeChart(events, dates, labels);
        _statusPie = BuildStatusPie(events);
        _endpointRows = BuildEndpointRows(events);
    }

    private void BuildSummary(List<EventProjection> events)
    {
        _totalRequests = events.Count;

        if (events.Count == 0)
        {
            _errorRatePct = 0;
            _avgLatencyMs = 0;
            _p95LatencyMs = 0;
            return;
        }

        var errors = events.Count(e => e.StatusCode >= 400);
        _errorRatePct = Math.Round((double)errors / events.Count * 100, 1);
        _avgLatencyMs = (int)Math.Round(events.Average(e => e.LatencyMs));
        _p95LatencyMs = Percentile(events.Select(e => e.LatencyMs).ToList(), 95);
    }

    private static int Percentile(List<int> values, int percentile)
    {
        if (values.Count == 0)
            return 0;
        values.Sort();
        var rank = (int)Math.Ceiling(percentile / 100.0 * values.Count) - 1;
        rank = Math.Clamp(rank, 0, values.Count - 1);
        return values[rank];
    }

    private static ChartData BuildVolumeChart(List<EventProjection> events, List<DateTime> dates, List<string> labels)
    {
        var classes = new[] { "2xx", "3xx", "4xx", "5xx" };

        var grouped = events
            .GroupBy(e => new { Class = StatusClass(e.StatusCode), Date = e.LocalDate })
            .ToDictionary(g => g.Key, g => g.Count());

        var series = classes
            .Where(c => events.Any(e => StatusClass(e.StatusCode) == c))
            .Select(c => new ChartSeries
            {
                Label = c,
                Values = dates
                    .Select(date => (decimal)grouped.GetValueOrDefault(new { Class = c, Date = date }, 0))
                    .ToList()
            })
            .ToList();

        return new ChartData { Series = series, Labels = labels };
    }

    private static PieData BuildStatusPie(List<EventProjection> events)
    {
        var grouped = events
            .GroupBy(e => StatusClass(e.StatusCode))
            .Select(g => new { Class = g.Key, Count = g.Count() })
            .OrderBy(g => g.Class)
            .ToList();

        return new PieData
        {
            Data = grouped.Select(g => new ChartSegment { Label = g.Class, Value = g.Count }).ToList()
        };
    }

    private static List<EndpointRow> BuildEndpointRows(List<EventProjection> events)
    {
        return events
            .GroupBy(e => new { e.Method, e.RouteTemplate })
            .Select(g => new EndpointRow
            {
                Method = g.Key.Method,
                RouteTemplate = string.IsNullOrEmpty(g.Key.RouteTemplate) ? "/" : g.Key.RouteTemplate,
                Count = g.Count(),
                ErrorRatePct = Math.Round((double)g.Count(e => e.StatusCode >= 400) / g.Count() * 100, 1),
                AvgLatencyMs = (int)Math.Round(g.Average(e => e.LatencyMs))
            })
            .OrderByDescending(r => r.Count)
            .Take(MaxEndpoints)
            .ToList();
    }

    private static string StatusClass(int statusCode) => statusCode switch
    {
        >= 200 and < 300 => "2xx",
        >= 300 and < 400 => "3xx",
        >= 400 and < 500 => "4xx",
        >= 500 => "5xx",
        _ => "other"
    };

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

    private class EventProjection
    {
        public DateTime IngestedAt { get; set; }
        public DateTime LocalDate { get; set; }
        public string RouteTemplate { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public int LatencyMs { get; set; }
    }

    private class EndpointRow
    {
        public string Method { get; set; } = string.Empty;
        public string RouteTemplate { get; set; } = string.Empty;
        public int Count { get; set; }
        public double ErrorRatePct { get; set; }
        public int AvgLatencyMs { get; set; }
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
