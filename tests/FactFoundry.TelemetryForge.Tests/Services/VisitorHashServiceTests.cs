using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using FactFoundry.TelemetryForge.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace FactFoundry.TelemetryForge.Tests.Services;

public class VisitorHashServiceTests
{
    private static TelemetryForgeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TelemetryForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TelemetryForgeDbContext(options);
    }

    [Fact]
    public async Task IsFirstSeenAsync_ReturnsTrueForNewHash()
    {
        using var db = CreateDb();
        var service = new VisitorHashService(db);

        var result = await service.IsFirstSeenAsync("abc123", HashType.Fingerprint, SiteType.Desktop, "site-1");

        Assert.True(result);
    }

    [Fact]
    public async Task IsFirstSeenAsync_ReturnsFalseForExistingHash()
    {
        using var db = CreateDb();
        var service = new VisitorHashService(db);

        await service.IsFirstSeenAsync("abc123", HashType.Fingerprint, SiteType.Desktop, "site-1");
        var result = await service.IsFirstSeenAsync("abc123", HashType.Fingerprint, SiteType.Desktop, "site-1");

        Assert.False(result);
    }

    [Fact]
    public async Task IsFirstSeenAsync_InsertsRecordIntoDatabase()
    {
        using var db = CreateDb();
        var service = new VisitorHashService(db);

        await service.IsFirstSeenAsync("abc123", HashType.Ga, SiteType.Web, "site-1");

        var record = await db.VisitorHashes.SingleAsync();
        Assert.Equal("abc123", record.Hash);
        Assert.Equal(HashType.Ga, record.HashType);
        Assert.Equal(SiteType.Web, record.SourceType);
        Assert.Equal("site-1", record.SiteId);
    }

    [Fact]
    public async Task IsFirstSeenAsync_SameHashDifferentSites_BothReturnTrue()
    {
        using var db = CreateDb();
        var service = new VisitorHashService(db);

        var result1 = await service.IsFirstSeenAsync("abc123", HashType.Ip, SiteType.Web, "site-1");
        var result2 = await service.IsFirstSeenAsync("abc123", HashType.Ip, SiteType.Web, "site-2");

        Assert.True(result1);
        Assert.True(result2);
    }

    [Fact]
    public async Task IsFirstSeenAsync_DifferentHashesSameSite_BothReturnTrue()
    {
        using var db = CreateDb();
        var service = new VisitorHashService(db);

        var result1 = await service.IsFirstSeenAsync("hash-a", HashType.Fingerprint, SiteType.Desktop, "site-1");
        var result2 = await service.IsFirstSeenAsync("hash-b", HashType.Fingerprint, SiteType.Desktop, "site-1");

        Assert.True(result1);
        Assert.True(result2);
    }

    [Fact]
    public async Task IsFirstSeenAsync_SetsFirstSeenTimestamp()
    {
        using var db = CreateDb();
        var service = new VisitorHashService(db);
        var before = DateTime.UtcNow;

        await service.IsFirstSeenAsync("abc123", HashType.VendorId, SiteType.Mobile, "site-1");

        var record = await db.VisitorHashes.SingleAsync();
        Assert.True(record.FirstSeen >= before);
        Assert.True(record.FirstSeen <= DateTime.UtcNow);
    }
}
