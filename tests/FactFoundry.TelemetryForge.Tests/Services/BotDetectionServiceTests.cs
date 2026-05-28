using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using FactFoundry.TelemetryForge.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace FactFoundry.TelemetryForge.Tests.Services;

/// <summary>
/// Tests for the retroactive BotDetectionService scan.
/// </summary>
public class BotDetectionServiceTests
{
    private static TelemetryForgeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TelemetryForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TelemetryForgeDbContext(options);
    }

    private static WebEvent CreateEvent(string sessionHash, string page, DateTimeOffset? timestamp = null,
        string eventType = "page_view", string? country = "US", bool isBot = false, string? botReason = null,
        string? language = "en-US", string? deviceType = "desktop")
    {
        return new WebEvent
        {
            SiteId = "site-1",
            SiteName = "Test",
            SessionHash = sessionHash,
            Page = page,
            EventType = eventType,
            Language = language ?? string.Empty,
            Country = country,
            IsBot = isBot,
            BotReason = botReason,
            DeviceType = deviceType,
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
            IngestedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task Scan_DetectsPageVelocity()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        db.WebEvents.AddRange(
            CreateEvent("session-a", "/p1", now.AddSeconds(-4)),
            CreateEvent("session-a", "/p2", now.AddSeconds(-3)),
            CreateEvent("session-a", "/p3", now.AddSeconds(-2)),
            CreateEvent("session-a", "/p4", now.AddSeconds(-1)),
            CreateEvent("session-a", "/p5", now));
        await db.SaveChangesAsync();

        var service = new BotDetectionService(db);
        var result = await service.ScanAsync();

        Assert.Equal(5, result.NewlyFlagged);
        var events = await db.WebEvents.Where(e => e.SessionHash == "session-a").ToListAsync();
        Assert.All(events, e => Assert.True(e.IsBot));
        Assert.All(events, e => Assert.Equal("page-velocity", e.BotReason));
    }

    [Fact]
    public async Task Scan_DetectsPathScan()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        db.WebEvents.AddRange(
            CreateEvent("session-b", "/dir1/wp-includes/wlwmanifest.xml", now.AddMinutes(-10)),
            CreateEvent("session-b", "/dir2/wp-includes/wlwmanifest.xml", now.AddMinutes(-8)),
            CreateEvent("session-b", "/dir3/wp-includes/wlwmanifest.xml", now.AddMinutes(-6)),
            CreateEvent("session-b", "/dir4/wp-includes/wlwmanifest.xml", now.AddMinutes(-4)),
            CreateEvent("session-b", "/dir5/wp-includes/wlwmanifest.xml", now.AddMinutes(-2)));
        await db.SaveChangesAsync();

        var service = new BotDetectionService(db);
        var result = await service.ScanAsync();

        Assert.Equal(5, result.NewlyFlagged);
        var events = await db.WebEvents.Where(e => e.SessionHash == "session-b").ToListAsync();
        Assert.All(events, e => Assert.Equal("path-scan", e.BotReason));
    }

    [Fact]
    public async Task Scan_DetectsCountryHop()
    {
        using var db = CreateDb();
        db.WebEvents.AddRange(
            CreateEvent("session-c", "/", country: "US"),
            CreateEvent("session-c", "/about", country: "JP"),
            CreateEvent("session-c", "/contact", country: "PL"));
        await db.SaveChangesAsync();

        var service = new BotDetectionService(db);
        var result = await service.ScanAsync();

        Assert.Equal(3, result.NewlyFlagged);
        var events = await db.WebEvents.Where(e => e.SessionHash == "session-c").ToListAsync();
        Assert.All(events, e => Assert.Equal("country-hop", e.BotReason));
    }

    [Fact]
    public async Task Scan_BackfillsReason_UserAgent()
    {
        using var db = CreateDb();
        db.WebEvents.AddRange(
            CreateEvent("session-d", "/", isBot: true, botReason: null, deviceType: "bot"),
            CreateEvent("session-d", "/about", isBot: true, botReason: null, deviceType: "bot"));
        await db.SaveChangesAsync();

        var service = new BotDetectionService(db);
        var result = await service.ScanAsync();

        Assert.Equal(0, result.NewlyFlagged);
        Assert.Equal(2, result.ReasonsBackfilled);
        var events = await db.WebEvents.Where(e => e.SessionHash == "session-d").ToListAsync();
        Assert.All(events, e => Assert.Equal("user-agent", e.BotReason));
    }

    [Fact]
    public async Task Scan_BackfillsReason_NoLanguage()
    {
        using var db = CreateDb();
        db.WebEvents.AddRange(
            CreateEvent("session-e", "/", isBot: true, botReason: null, language: "", deviceType: "desktop"),
            CreateEvent("session-e", "/about", isBot: true, botReason: null, language: "", deviceType: "desktop"));
        await db.SaveChangesAsync();

        var service = new BotDetectionService(db);
        var result = await service.ScanAsync();

        Assert.Equal(0, result.NewlyFlagged);
        Assert.Equal(2, result.ReasonsBackfilled);
        var events = await db.WebEvents.Where(e => e.SessionHash == "session-e").ToListAsync();
        Assert.All(events, e => Assert.Equal("no-language", e.BotReason));
    }

    [Fact]
    public async Task Scan_SkipsAlreadyClassified()
    {
        using var db = CreateDb();
        db.WebEvents.AddRange(
            CreateEvent("session-f", "/", isBot: true, botReason: "user-agent"),
            CreateEvent("session-f", "/about", isBot: true, botReason: "user-agent"));
        await db.SaveChangesAsync();

        var service = new BotDetectionService(db);
        var result = await service.ScanAsync();

        Assert.Equal(0, result.NewlyFlagged);
        Assert.Equal(0, result.ReasonsBackfilled);
    }

    [Fact]
    public async Task Scan_DoesNotFlagLegitimateTraffic()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        db.WebEvents.AddRange(
            CreateEvent("session-g", "/", now.AddMinutes(-5)),
            CreateEvent("session-g", "/about", now.AddMinutes(-3)),
            CreateEvent("session-g", "/contact", now));
        await db.SaveChangesAsync();

        var service = new BotDetectionService(db);
        var result = await service.ScanAsync();

        Assert.Equal(0, result.NewlyFlagged);
        var events = await db.WebEvents.Where(e => e.SessionHash == "session-g").ToListAsync();
        Assert.All(events, e => Assert.False(e.IsBot));
    }
}
