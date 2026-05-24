using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using FactFoundry.TelemetryForge.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace FactFoundry.TelemetryForge.Tests.Services;

public class ApiKeyServiceTests
{
    private static TelemetryForgeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TelemetryForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TelemetryForgeDbContext(options);
    }

    [Fact]
    public void GenerateKey_ReturnsKeyWithCorrectPrefix()
    {
        using var db = CreateDb();
        var service = new ApiKeyService(db);

        var (rawKey, _) = service.GenerateKey();

        Assert.StartsWith("tfrg_live_", rawKey);
    }

    [Fact]
    public void GenerateKey_ReturnsKeyWithExpectedLength()
    {
        using var db = CreateDb();
        var service = new ApiKeyService(db);

        var (rawKey, _) = service.GenerateKey();

        // "tfrg_live_" (10 chars) + 32 bytes as hex (64 chars) = 74 chars
        Assert.Equal(74, rawKey.Length);
    }

    [Fact]
    public void GenerateKey_ReturnsDifferentKeysEachCall()
    {
        using var db = CreateDb();
        var service = new ApiKeyService(db);

        var (key1, _) = service.GenerateKey();
        var (key2, _) = service.GenerateKey();

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void GenerateKey_HashVerifiesAgainstRawKey()
    {
        using var db = CreateDb();
        var service = new ApiKeyService(db);

        var (rawKey, hash) = service.GenerateKey();

        Assert.True(BCrypt.Net.BCrypt.Verify(rawKey, hash));
    }

    [Fact]
    public async Task ValidateKeyAsync_ReturnsNullForUnknownKey()
    {
        using var db = CreateDb();
        var service = new ApiKeyService(db);

        var result = await service.ValidateKeyAsync("tfrg_live_0000000000000000000000000000000000000000000000000000000000000000");

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateKeyAsync_ReturnsSiteIdForValidKey()
    {
        using var db = CreateDb();
        var service = new ApiKeyService(db);

        var (rawKey, hash) = service.GenerateKey();
        db.Sites.Add(new Site
        {
            Id = "site-1",
            Name = "Test Site",
            Type = SiteType.Web,
            ApiKeyHash = hash,
            RegisteredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await service.ValidateKeyAsync(rawKey);

        Assert.Equal("site-1", result);
    }

    [Fact]
    public async Task ValidateKeyAsync_ReturnsNullForWrongKey()
    {
        using var db = CreateDb();
        var service = new ApiKeyService(db);

        var (_, hash) = service.GenerateKey();
        db.Sites.Add(new Site
        {
            Id = "site-1",
            Name = "Test Site",
            Type = SiteType.Web,
            ApiKeyHash = hash,
            RegisteredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var (differentKey, _) = service.GenerateKey();
        var result = await service.ValidateKeyAsync(differentKey);

        Assert.Null(result);
    }
}
