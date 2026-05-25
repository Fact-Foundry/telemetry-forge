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
    public async Task PublishAsync_WebEvent_StoresWebSession()
    {
        using var db = CreateDb();
        var publisher = new DatabaseEventPublisher(db, NullLogger<DatabaseEventPublisher>.Instance);

        var webEvent = new EnrichedWebEvent
        {
            SiteId = "site-1",
            SiteName = "My Site",
            Platform = "Blazor",
            SessionStart = DateTime.UtcNow.AddMinutes(-5),
            SessionEnd = DateTime.UtcNow,
            DurationMs = 300000,
            SessionHash = "abc123",
            IsFirstVisit = true,
            Language = "en-US",
            EntryPage = "/",
            ExitPage = "/about",
            PagePath = ["/", "/about"],
            PageCount = 2,
            StatusCodes = new Dictionary<string, int> { ["200"] = 2 }
        };

        await publisher.PublishAsync(webEvent);

        var session = await db.WebSessions.SingleAsync();
        Assert.Equal("site-1", session.SiteId);
        Assert.Equal("My Site", session.SiteName);
        Assert.True(session.IsFirstVisit);
        Assert.Equal(2, session.PageCount);
        Assert.Equal("/", session.EntryPage);
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
            IsFirstInstall = true,
            LicenseTier = "commercial",
            SessionStart = DateTime.UtcNow.AddMinutes(-10),
            SessionEnd = DateTime.UtcNow,
            DurationMs = 600000,
            FeaturePath = ["ModelEditor", "Export"],
            ErrorEvents = [new ErrorEvent { Feature = "Export", Message = "Timeout", Timestamp = DateTime.UtcNow }]
        };

        await publisher.PublishAsync(desktopEvent);

        var session = await db.DesktopSessions.SingleAsync();
        Assert.Equal("app-1", session.SiteId);
        Assert.Equal("Semantic Modeler", session.AppName);
        Assert.True(session.IsFirstInstall);
        Assert.Equal("commercial", session.LicenseTier);
        Assert.Equal(2, session.FeatureCount);
        Assert.Equal(1, session.ErrorCount);
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
            IsFirstInstall = false,
            SessionStart = DateTime.UtcNow.AddMinutes(-3),
            SessionEnd = DateTime.UtcNow,
            DurationMs = 180000,
            FeaturePath = ["Dashboard", "Scanner"],
            ErrorEvents = []
        };

        await publisher.PublishAsync(mobileEvent);

        var session = await db.MobileSessions.SingleAsync();
        Assert.Equal("app-2", session.SiteId);
        Assert.Equal("Field Companion", session.AppName);
        Assert.False(session.IsFirstInstall);
        Assert.Equal("vendor_id", session.DeviceHashType);
        Assert.Equal(2, session.FeatureCount);
        Assert.Equal(0, session.ErrorCount);
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
            Platform = "Blazor",
            SessionHash = "hash",
            Language = "en",
            EntryPage = "/",
            ExitPage = "/",
            PagePath = [],
            StatusCodes = []
        });

        var session = await db.WebSessions.SingleAsync();
        Assert.True(session.IngestedAt >= before);
        Assert.True(session.IngestedAt <= DateTime.UtcNow);
    }
}
