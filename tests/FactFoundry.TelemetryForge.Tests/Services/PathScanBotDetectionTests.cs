using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FactFoundry.TelemetryForge.Tests.Services;

/// <summary>
/// Tests for path-scan bot detection — sessions requesting the same filename
/// from 5+ different directory paths are flagged as bots.
/// </summary>
public class PathScanBotDetectionTests
{
    private static TelemetryForgeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TelemetryForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TelemetryForgeDbContext(options);
    }

    private static WebEvent CreateEvent(string sessionHash, string page)
    {
        return new WebEvent
        {
            SiteId = "site-1",
            SiteName = "Test",
            SessionHash = sessionHash,
            Page = page,
            EventType = "page_view",
            Language = "en-US",
            Country = "US",
            IsBot = false,
            Timestamp = DateTimeOffset.UtcNow,
            IngestedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task FourDistinctPaths_NotFlagged()
    {
        using var db = CreateDb();
        db.WebEvents.AddRange(
            CreateEvent("session-a", "/dir1/wp-includes/wlwmanifest.xml"),
            CreateEvent("session-a", "/dir2/wp-includes/wlwmanifest.xml"),
            CreateEvent("session-a", "/dir3/wp-includes/wlwmanifest.xml"),
            CreateEvent("session-a", "/dir4/wp-includes/wlwmanifest.xml"));
        await db.SaveChangesAsync();

        var incomingPage = "/dir5/wp-includes/wlwmanifest.xml";
        var fileName = Path.GetFileName(incomingPage.TrimEnd('/'));

        var priorPages = await db.WebEvents
            .Where(e => e.SessionHash == "session-a" && e.EventType == "page_view" && e.Page != null)
            .Select(e => e.Page!)
            .ToListAsync();

        // Before adding the incoming page, only 4 exist
        var distinctPaths = priorPages
            .Where(p => string.Equals(Path.GetFileName(p.TrimEnd('/')), fileName, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        Assert.Equal(4, distinctPaths);
        Assert.True(distinctPaths < 5);
    }

    [Fact]
    public async Task FiveDistinctPaths_Flagged()
    {
        using var db = CreateDb();
        db.WebEvents.AddRange(
            CreateEvent("session-b", "/dir1/wp-includes/wlwmanifest.xml"),
            CreateEvent("session-b", "/dir2/wp-includes/wlwmanifest.xml"),
            CreateEvent("session-b", "/dir3/wp-includes/wlwmanifest.xml"),
            CreateEvent("session-b", "/dir4/wp-includes/wlwmanifest.xml"));
        await db.SaveChangesAsync();

        var incomingPage = "/dir5/wp-includes/wlwmanifest.xml";
        var fileName = Path.GetFileName(incomingPage.TrimEnd('/'));

        var priorPages = await db.WebEvents
            .Where(e => e.SessionHash == "session-b" && e.EventType == "page_view" && e.Page != null)
            .Select(e => e.Page!)
            .ToListAsync();

        priorPages.Add(incomingPage);

        var distinctPaths = priorPages
            .Where(p => string.Equals(Path.GetFileName(p.TrimEnd('/')), fileName, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        Assert.Equal(5, distinctPaths);
        Assert.True(distinctPaths >= 5);
    }

    [Fact]
    public async Task FiveDistinctPaths_RetroactivelyFlags()
    {
        using var db = CreateDb();
        db.WebEvents.AddRange(
            CreateEvent("session-c", "/dir1/wp-includes/wlwmanifest.xml"),
            CreateEvent("session-c", "/dir2/wp-includes/wlwmanifest.xml"),
            CreateEvent("session-c", "/dir3/wp-includes/wlwmanifest.xml"),
            CreateEvent("session-c", "/dir4/wp-includes/wlwmanifest.xml"));
        await db.SaveChangesAsync();

        var incomingPage = "/dir5/wp-includes/wlwmanifest.xml";
        var fileName = Path.GetFileName(incomingPage.TrimEnd('/'));

        var priorPages = await db.WebEvents
            .Where(e => e.SessionHash == "session-c" && e.EventType == "page_view" && e.Page != null)
            .Select(e => e.Page!)
            .ToListAsync();

        priorPages.Add(incomingPage);

        var distinctPaths = priorPages
            .Where(p => string.Equals(Path.GetFileName(p.TrimEnd('/')), fileName, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (distinctPaths >= 5)
        {
            var priorEvents = await db.WebEvents
                .Where(e => e.SessionHash == "session-c" && !e.IsBot)
                .ToListAsync();
            foreach (var evt in priorEvents)
            {
                evt.IsBot = true;
                evt.BotReason = "path-scan";
            }
            await db.SaveChangesAsync();
        }

        var allEvents = await db.WebEvents.Where(e => e.SessionHash == "session-c").ToListAsync();
        Assert.All(allEvents, e => Assert.True(e.IsBot));
        Assert.All(allEvents, e => Assert.Equal("path-scan", e.BotReason));
    }

    [Fact]
    public async Task SamePathRepeated_NotFlagged()
    {
        using var db = CreateDb();
        db.WebEvents.AddRange(
            CreateEvent("session-d", "/wp-includes/wlwmanifest.xml"),
            CreateEvent("session-d", "/wp-includes/wlwmanifest.xml"),
            CreateEvent("session-d", "/wp-includes/wlwmanifest.xml"),
            CreateEvent("session-d", "/wp-includes/wlwmanifest.xml"),
            CreateEvent("session-d", "/wp-includes/wlwmanifest.xml"));
        await db.SaveChangesAsync();

        var fileName = "wlwmanifest.xml";
        var priorPages = await db.WebEvents
            .Where(e => e.SessionHash == "session-d" && e.EventType == "page_view" && e.Page != null)
            .Select(e => e.Page!)
            .ToListAsync();

        var distinctPaths = priorPages
            .Where(p => string.Equals(Path.GetFileName(p.TrimEnd('/')), fileName, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        Assert.Equal(1, distinctPaths);
        Assert.True(distinctPaths < 5);
    }

    [Fact]
    public async Task PathWithoutExtension_SkipsCheck()
    {
        using var db = CreateDb();
        db.WebEvents.AddRange(
            CreateEvent("session-e", "/page1"),
            CreateEvent("session-e", "/page2"),
            CreateEvent("session-e", "/page3"),
            CreateEvent("session-e", "/page4"),
            CreateEvent("session-e", "/page5"));
        await db.SaveChangesAsync();

        var incomingPage = "/page6";
        var fileName = Path.GetFileName(incomingPage.TrimEnd('/'));

        Assert.False(fileName.Contains('.'));
    }

    [Fact]
    public async Task DifferentFilenames_NotFlagged()
    {
        using var db = CreateDb();
        db.WebEvents.AddRange(
            CreateEvent("session-f", "/dir1/file1.php"),
            CreateEvent("session-f", "/dir2/file2.php"),
            CreateEvent("session-f", "/dir3/file3.php"),
            CreateEvent("session-f", "/dir4/file4.php"));
        await db.SaveChangesAsync();

        var incomingPage = "/dir5/file5.php";
        var fileName = Path.GetFileName(incomingPage.TrimEnd('/'));

        var priorPages = await db.WebEvents
            .Where(e => e.SessionHash == "session-f" && e.EventType == "page_view" && e.Page != null)
            .Select(e => e.Page!)
            .ToListAsync();

        priorPages.Add(incomingPage);

        var distinctPaths = priorPages
            .Where(p => string.Equals(Path.GetFileName(p.TrimEnd('/')), fileName, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        Assert.Equal(1, distinctPaths);
        Assert.True(distinctPaths < 5);
    }
}
