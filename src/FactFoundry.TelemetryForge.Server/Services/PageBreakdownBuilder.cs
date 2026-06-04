namespace FactFoundry.TelemetryForge.Server.Services;

/// <summary>
/// A single page-view event projected for the page-breakdown pivot.
/// </summary>
/// <param name="Page">Page path (empty is normalized to "/").</param>
/// <param name="Os">Operating system (null/empty normalized to "Unknown").</param>
/// <param name="Browser">Browser (null/empty normalized to "Unknown").</param>
/// <param name="LocalDate">The event date in the display timezone (date only).</param>
public sealed record PageBreakdownEvent(string Page, string? Os, string? Browser, DateTime LocalDate);

/// <summary>
/// One pivot row: a page / OS / browser combination with per-date visit counts and a total.
/// </summary>
/// <param name="Page">Page path.</param>
/// <param name="Os">Operating system.</param>
/// <param name="Browser">Browser.</param>
/// <param name="Counts">Visit count keyed by date.</param>
/// <param name="Total">Total visits across all dates.</param>
public sealed record PageBreakdownRow(
    string Page,
    string Os,
    string Browser,
    IReadOnlyDictionary<DateTime, int> Counts,
    int Total);

/// <summary>
/// Builds the page × OS × browser × date visit pivot used by the Analytics "Page Breakdown" tab.
/// Visits are raw page-view counts (every load), not unique sessions.
/// </summary>
public static class PageBreakdownBuilder
{
    /// <summary>
    /// Groups page-view events into one row per (page, OS, browser), counting visits per date.
    /// Rows are ordered by total visits descending, then page / OS / browser alphabetically.
    /// </summary>
    /// <param name="events">The page-view events to aggregate.</param>
    /// <param name="dates">The date range (used by callers for column rendering; not required here).</param>
    /// <returns>The ordered pivot rows.</returns>
    public static IReadOnlyList<PageBreakdownRow> Build(
        IEnumerable<PageBreakdownEvent> events,
        IReadOnlyList<DateTime> dates)
    {
        return events
            .GroupBy(e => new
            {
                Page = string.IsNullOrEmpty(e.Page) ? "/" : e.Page,
                Os = string.IsNullOrEmpty(e.Os) ? "Unknown" : e.Os,
                Browser = string.IsNullOrEmpty(e.Browser) ? "Unknown" : e.Browser
            })
            .Select(g =>
            {
                var counts = g
                    .GroupBy(e => e.LocalDate)
                    .ToDictionary(d => d.Key, d => d.Count());
                return new PageBreakdownRow(
                    g.Key.Page, g.Key.Os, g.Key.Browser, counts, counts.Values.Sum());
            })
            .OrderByDescending(r => r.Total)
            .ThenBy(r => r.Page, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Os, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Browser, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
