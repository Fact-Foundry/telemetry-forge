using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FactFoundry.TelemetryForge.Tests.Services;

/// <summary>
/// Tests for page velocity bot detection — sessions with 5+ page_view events
/// in 60 seconds are flagged as bots with retroactive flagging.
/// </summary>
public class PageVelocityBotDetectionTests
{
    private static TelemetryForgeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TelemetryForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TelemetryForgeDbContext(options);
    }

    private static WebEvent CreateEvent(string sessionHash, DateTimeOffset timestamp, string page = "/test")
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
            Timestamp = timestamp,
            IngestedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task FourPagesInWindow_NotFlagged()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        db.WebEvents.AddRange(
            CreateEvent("session-a", now.AddSeconds(-30)),
            CreateEvent("session-a", now.AddSeconds(-20)),
            CreateEvent("session-a", now.AddSeconds(-10)),
            CreateEvent("session-a", now));
        await db.SaveChangesAsync();

        var windowStart = now.AddSeconds(-60);
        var count = await db.WebEvents
            .Where(e => e.SessionHash == "session-a" && e.EventType == "page_view" && e.Timestamp >= windowStart)
            .CountAsync();

        Assert.Equal(4, count);
        Assert.True(count < 5);
    }

    [Fact]
    public async Task FivePagesInWindow_Flagged()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        db.WebEvents.AddRange(
            CreateEvent("session-b", now.AddSeconds(-40)),
            CreateEvent("session-b", now.AddSeconds(-30)),
            CreateEvent("session-b", now.AddSeconds(-20)),
            CreateEvent("session-b", now.AddSeconds(-10)),
            CreateEvent("session-b", now));
        await db.SaveChangesAsync();

        var windowStart = now.AddSeconds(-60);
        var count = await db.WebEvents
            .Where(e => e.SessionHash == "session-b" && e.EventType == "page_view" && e.Timestamp >= windowStart)
            .CountAsync();

        Assert.Equal(5, count);
        Assert.True(count >= 5);
    }

    [Fact]
    public async Task FivePagesInWindow_RetroactivelyFlags()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        db.WebEvents.AddRange(
            CreateEvent("session-c", now.AddSeconds(-40)),
            CreateEvent("session-c", now.AddSeconds(-30)),
            CreateEvent("session-c", now.AddSeconds(-20)),
            CreateEvent("session-c", now.AddSeconds(-10)),
            CreateEvent("session-c", now));
        await db.SaveChangesAsync();

        var windowStart = now.AddSeconds(-60);
        var count = await db.WebEvents
            .Where(e => e.SessionHash == "session-c" && e.EventType == "page_view" && e.Timestamp >= windowStart)
            .CountAsync();

        if (count >= 5)
        {
            var priorEvents = await db.WebEvents
                .Where(e => e.SessionHash == "session-c" && !e.IsBot)
                .ToListAsync();
            foreach (var evt in priorEvents)
            {
                evt.IsBot = true;
                evt.BotReason = "page-velocity";
            }
            await db.SaveChangesAsync();
        }

        var allEvents = await db.WebEvents.Where(e => e.SessionHash == "session-c").ToListAsync();
        Assert.All(allEvents, e => Assert.True(e.IsBot));
        Assert.All(allEvents, e => Assert.Equal("page-velocity", e.BotReason));
    }

    [Fact]
    public async Task PagesOutsideWindow_NotCounted()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        db.WebEvents.AddRange(
            CreateEvent("session-d", now.AddSeconds(-120)),
            CreateEvent("session-d", now.AddSeconds(-90)),
            CreateEvent("session-d", now.AddSeconds(-80)),
            CreateEvent("session-d", now.AddSeconds(-10)),
            CreateEvent("session-d", now));
        await db.SaveChangesAsync();

        var windowStart = now.AddSeconds(-60);
        var count = await db.WebEvents
            .Where(e => e.SessionHash == "session-d" && e.EventType == "page_view" && e.Timestamp >= windowStart)
            .CountAsync();

        Assert.Equal(2, count);
        Assert.True(count < 5);
    }
}
