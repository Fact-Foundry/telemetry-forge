using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Models.Events;
using FactFoundry.TelemetryForge.Server.Models.Payloads;
using FactFoundry.TelemetryForge.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FactFoundry.TelemetryForge.Tests.Services;

public class DatabaseEventPublisherTests
{
    private static TelemetryForgeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TelemetryForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TelemetryForgeDbContext(options);
    }

    [Fact]
    public async Task PublishAsync_WebEvent_StoresWebEvent()
    {
        using var db = CreateDb();
        var publisher = new DatabaseEventPublisher(db, NullLogger<DatabaseEventPublisher>.Instance);

        var webEvent = new EnrichedWebEvent
        {
            SiteId = "site-1",
            SiteName = "My Site",
            SessionHash = "abc123",
            IsFirstVisit = true,
            Page = "/about",
            StatusCode = 200,
            EventType = "page_view",
            Language = "en-US",
            Timestamp = DateTimeOffset.UtcNow
        };

        await publisher.PublishAsync(webEvent);

        var stored = await db.WebEvents.SingleAsync();
        Assert.Equal("site-1", stored.SiteId);
        Assert.Equal("My Site", stored.SiteName);
        Assert.True(stored.IsFirstVisit);
        Assert.Equal("/about", stored.Page);
        Assert.Equal(200, stored.StatusCode);
        Assert.Equal("page_view", stored.EventType);
    }

    [Fact]
    public async Task PublishAsync_WebCustomEvent_StoresEventNameAndData()
    {
        using var db = CreateDb();
        var publisher = new DatabaseEventPublisher(db, NullLogger<DatabaseEventPublisher>.Instance);

        var webEvent = new EnrichedWebEvent
        {
            SiteId = "site-1",
            SiteName = "My Site",
            SessionHash = "abc123",
            Page = "/contact",
            EventType = "custom",
            EventName = "form_submit",
            EventData = new Dictionary<string, object> { ["subject"] = "inquiry" },
            Language = "en-US",
            Timestamp = DateTimeOffset.UtcNow
        };

        await publisher.PublishAsync(webEvent);

        var stored = await db.WebEvents.SingleAsync();
        Assert.Equal("custom", stored.EventType);
        Assert.Equal("form_submit", stored.EventName);
        Assert.NotNull(stored.EventData);
        Assert.Contains("inquiry", stored.EventData);
    }

    [Fact]
    public async Task PublishAsync_DesktopEvent_StoresDesktopSession()
    {
        using var db = CreateDb();
        var publisher = new DatabaseEventPublisher(db, NullLogger<DatabaseEventPublisher>.Instance);

        var desktopEvent = new EnrichedDesktopEvent
        {
            AppId = "app-1",
            AppName = "Semantic Modeler",
            AppVersion = "2.1.0",
            Platform = "Windows",
            OsVersion = "11.0",
            FingerprintHash = "fp-hash-123",
            SessionId = "session-uuid-1",
            Sequence = 0,
            IsFirstInstall = true,
            SessionStart = DateTimeOffset.UtcNow.AddMinutes(-10),
            SessionEnd = DateTimeOffset.UtcNow,
            DurationMs = 600000,
            FeaturePath = ["ModelEditor", "Export"],
            ErrorEvents = [new ErrorEvent { Feature = "Export", Message = "Timeout", Timestamp = DateTimeOffset.UtcNow }]
        };

        await publisher.PublishAsync(desktopEvent);

        var session = await db.DesktopSessions.SingleAsync();
        Assert.Equal("app-1", session.SiteId);
        Assert.Equal("session-uuid-1", session.SessionId);
        Assert.Equal("Semantic Modeler", session.AppName);
        Assert.True(session.IsFirstInstall);
        Assert.Equal(2, session.FeatureCount);
        Assert.Equal(1, session.ErrorCount);
    }

    [Fact]
    public async Task PublishAsync_DesktopHeartbeat_AppendsToExistingSession()
    {
        using var db = CreateDb();
        var publisher = new DatabaseEventPublisher(db, NullLogger<DatabaseEventPublisher>.Instance);
        var sessionId = "heartbeat-session-1";

        var first = new EnrichedDesktopEvent
        {
            AppId = "app-1",
            AppName = "Semantic Modeler",
            AppVersion = "2.1.0",
            Platform = "Windows",
            OsVersion = "11.0",
            FingerprintHash = "fp-hash-123",
            SessionId = sessionId,
            Sequence = 0,
            IsFirstInstall = false,
            SessionStart = DateTimeOffset.UtcNow.AddMinutes(-30),
            SessionEnd = DateTimeOffset.UtcNow.AddMinutes(-15),
            DurationMs = 900000,
            FeaturePath = ["ModelEditor"],
            ErrorEvents = []
        };

        await publisher.PublishAsync(first);

        var second = new EnrichedDesktopEvent
        {
            AppId = "app-1",
            AppName = "Semantic Modeler",
            AppVersion = "2.1.0",
            Platform = "Windows",
            OsVersion = "11.0",
            FingerprintHash = "fp-hash-123",
            SessionId = sessionId,
            Sequence = 1,
            IsFirstInstall = false,
            SessionStart = DateTimeOffset.UtcNow.AddMinutes(-30),
            SessionEnd = DateTimeOffset.UtcNow,
            DurationMs = 1800000,
            FeaturePath = ["Export", "Settings"],
            ErrorEvents = [new ErrorEvent { Feature = "Export", Message = "Failed", Timestamp = DateTimeOffset.UtcNow }]
        };

        await publisher.PublishAsync(second);

        var sessions = await db.DesktopSessions.ToListAsync();
        Assert.Single(sessions);

        var session = sessions[0];
        Assert.Equal(3, session.FeatureCount);
        Assert.Equal(1, session.ErrorCount);
        Assert.Equal(1800000, session.DurationMs);
        Assert.Equal(["ModelEditor", "Export", "Settings"], session.FeaturePath);
    }

    [Fact]
    public async Task PublishAsync_MobileEvent_StoresMobileSession()
    {
        using var db = CreateDb();
        var publisher = new DatabaseEventPublisher(db, NullLogger<DatabaseEventPublisher>.Instance);

        var mobileEvent = new EnrichedMobileEvent
        {
            AppId = "app-2",
            AppName = "Field Companion",
            AppVersion = "1.0.0",
            Platform = "iOS",
            OsVersion = "17.5",
            DeviceHash = "device-hash-456",
            DeviceHashType = "vendor_id",
            SessionId = "mobile-session-1",
            Sequence = 0,
            IsFirstInstall = false,
            SessionStart = DateTimeOffset.UtcNow.AddMinutes(-3),
            SessionEnd = DateTimeOffset.UtcNow,
            DurationMs = 180000,
            FeaturePath = ["Dashboard", "Scanner"],
            ErrorEvents = []
        };

        await publisher.PublishAsync(mobileEvent);

        var session = await db.MobileSessions.SingleAsync();
        Assert.Equal("app-2", session.SiteId);
        Assert.Equal("mobile-session-1", session.SessionId);
        Assert.Equal("Field Companion", session.AppName);
        Assert.False(session.IsFirstInstall);
        Assert.Equal("vendor_id", session.DeviceHashType);
        Assert.Equal(2, session.FeatureCount);
        Assert.Equal(0, session.ErrorCount);
    }

    [Fact]
    public async Task PublishAsync_MobileHeartbeat_AppendsToExistingSession()
    {
        using var db = CreateDb();
        var publisher = new DatabaseEventPublisher(db, NullLogger<DatabaseEventPublisher>.Instance);
        var sessionId = "mobile-heartbeat-1";

        await publisher.PublishAsync(new EnrichedMobileEvent
        {
            AppId = "app-2",
            AppName = "Field Companion",
            AppVersion = "1.0.0",
            Platform = "iOS",
            OsVersion = "17.5",
            DeviceHash = "device-hash-456",
            DeviceHashType = "vendor_id",
            SessionId = sessionId,
            Sequence = 0,
            IsFirstInstall = false,
            SessionStart = DateTimeOffset.UtcNow.AddMinutes(-30),
            SessionEnd = DateTimeOffset.UtcNow.AddMinutes(-15),
            DurationMs = 900000,
            FeaturePath = ["Dashboard"],
            ErrorEvents = []
        });

        await publisher.PublishAsync(new EnrichedMobileEvent
        {
            AppId = "app-2",
            AppName = "Field Companion",
            AppVersion = "1.0.0",
            Platform = "iOS",
            OsVersion = "17.5",
            DeviceHash = "device-hash-456",
            DeviceHashType = "vendor_id",
            SessionId = sessionId,
            Sequence = 1,
            IsFirstInstall = false,
            SessionStart = DateTimeOffset.UtcNow.AddMinutes(-30),
            SessionEnd = DateTimeOffset.UtcNow,
            DurationMs = 1800000,
            FeaturePath = ["Scanner"],
            ErrorEvents = []
        });

        var sessions = await db.MobileSessions.ToListAsync();
        Assert.Single(sessions);
        Assert.Equal(2, sessions[0].FeatureCount);
        Assert.Equal(["Dashboard", "Scanner"], sessions[0].FeaturePath);
    }

    [Fact]
    public async Task PublishAsync_WebEvent_StoresIsBotFlag()
    {
        using var db = CreateDb();
        var publisher = new DatabaseEventPublisher(db, NullLogger<DatabaseEventPublisher>.Instance);

        var botEvent = new EnrichedWebEvent
        {
            SiteId = "site-1",
            SiteName = "My Site",
            SessionHash = "bot-hash",
            Page = "/",
            EventType = "page_view",
            IsBot = true,
            Language = "",
            Timestamp = DateTimeOffset.UtcNow
        };

        var humanEvent = new EnrichedWebEvent
        {
            SiteId = "site-1",
            SiteName = "My Site",
            SessionHash = "human-hash",
            Page = "/about",
            EventType = "page_view",
            IsBot = false,
            Language = "en-US",
            Timestamp = DateTimeOffset.UtcNow
        };

        await publisher.PublishAsync(botEvent);
        await publisher.PublishAsync(humanEvent);

        var events = await db.WebEvents.OrderBy(e => e.SessionHash).ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.True(events[0].IsBot);
        Assert.False(events[1].IsBot);
    }

    [Fact]
    public async Task PublishAsync_SetsIngestedAtTimestamp()
    {
        using var db = CreateDb();
        var publisher = new DatabaseEventPublisher(db, NullLogger<DatabaseEventPublisher>.Instance);
        var before = DateTime.UtcNow;

        await publisher.PublishAsync(new EnrichedWebEvent
        {
            SiteId = "site-1",
            SiteName = "Test",
            SessionHash = "hash",
            Page = "/",
            Language = "en",
            Timestamp = DateTimeOffset.UtcNow
        });

        var webEvent = await db.WebEvents.SingleAsync();
        Assert.True(webEvent.IngestedAt >= before);
        Assert.True(webEvent.IngestedAt <= DateTime.UtcNow);
    }
}
