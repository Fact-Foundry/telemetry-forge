using FactFoundry.TelemetryForge.Server.Services;

namespace FactFoundry.TelemetryForge.Tests.Services;

public class PageBreakdownBuilderTests
{
    private static readonly DateTime D1 = new(2026, 6, 1);
    private static readonly DateTime D2 = new(2026, 6, 2);
    private static readonly List<DateTime> Dates = [D1, D2];

    [Fact]
    public void Build_CountsVisitsPerDateAndTotal()
    {
        var events = new[]
        {
            new PageBreakdownEvent("/home", "Windows", "Chrome", D1),
            new PageBreakdownEvent("/home", "Windows", "Chrome", D1),
            new PageBreakdownEvent("/home", "Windows", "Chrome", D2),
        };

        var rows = PageBreakdownBuilder.Build(events, Dates);

        var row = Assert.Single(rows);
        Assert.Equal("/home", row.Page);
        Assert.Equal(2, row.Counts[D1]);
        Assert.Equal(1, row.Counts[D2]);
        Assert.Equal(3, row.Total);
    }

    [Fact]
    public void Build_SeparatesByOsAndBrowser()
    {
        var events = new[]
        {
            new PageBreakdownEvent("/home", "Windows", "Chrome", D1),
            new PageBreakdownEvent("/home", "macOS", "Safari", D1),
            new PageBreakdownEvent("/home", "Windows", "Chrome", D1),
        };

        var rows = PageBreakdownBuilder.Build(events, Dates);

        Assert.Equal(2, rows.Count);
        // Ordered by total desc: Windows/Chrome (2) before macOS/Safari (1).
        Assert.Equal(("Windows", "Chrome", 2), (rows[0].Os, rows[0].Browser, rows[0].Total));
        Assert.Equal(("macOS", "Safari", 1), (rows[1].Os, rows[1].Browser, rows[1].Total));
    }

    [Fact]
    public void Build_NormalizesEmptyValues()
    {
        var events = new[]
        {
            new PageBreakdownEvent("", null, null, D1),
        };

        var row = Assert.Single(PageBreakdownBuilder.Build(events, Dates));

        Assert.Equal("/", row.Page);
        Assert.Equal("Unknown", row.Os);
        Assert.Equal("Unknown", row.Browser);
        Assert.Equal(1, row.Total);
    }

    [Fact]
    public void Build_OrdersByTotalDescending()
    {
        var events = new[]
        {
            new PageBreakdownEvent("/a", "Windows", "Chrome", D1),
            new PageBreakdownEvent("/b", "Windows", "Chrome", D1),
            new PageBreakdownEvent("/b", "Windows", "Chrome", D2),
        };

        var rows = PageBreakdownBuilder.Build(events, Dates);

        Assert.Equal("/b", rows[0].Page); // total 2
        Assert.Equal("/a", rows[1].Page); // total 1
    }

    [Fact]
    public void Build_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(PageBreakdownBuilder.Build([], Dates));
    }
}
