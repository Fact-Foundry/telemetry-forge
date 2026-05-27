using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace FactFoundry.TelemetryForge.Tests.Services;

/// <summary>
/// Tests for DataApiKeyService — CRUD operations and key validation.
/// </summary>
public class DataApiKeyServiceTests
{
    private static TelemetryForgeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TelemetryForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TelemetryForgeDbContext(options);
    }

    [Fact]
    public async Task CreateKeyAsync_ReturnsRawKeyWithPrefix()
    {
        using var db = CreateDb();
        var service = new DataApiKeyService(db);

        var (rawKey, entity) = await service.CreateKeyAsync("Test Key", ["site-1", "site-2"]);

        Assert.StartsWith("tfrg_data_", rawKey);
        Assert.NotEmpty(entity.Id);
        Assert.Equal("Test Key", entity.Name);
        Assert.Equal(["site-1", "site-2"], entity.SiteIds);
    }

    [Fact]
    public async Task CreateKeyAsync_StoresBcryptHash()
    {
        using var db = CreateDb();
        var service = new DataApiKeyService(db);

        var (rawKey, entity) = await service.CreateKeyAsync("Test", ["site-1"]);

        Assert.True(BCrypt.Net.BCrypt.Verify(rawKey, entity.ApiKeyHash));
    }

    [Fact]
    public async Task ValidateKeyAsync_ValidKey_ReturnsSiteIds()
    {
        using var db = CreateDb();
        var service = new DataApiKeyService(db);

        var (rawKey, _) = await service.CreateKeyAsync("Test", ["site-a", "site-b"]);
        var result = await service.ValidateKeyAsync(rawKey);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains("site-a", result);
        Assert.Contains("site-b", result);
    }

    [Fact]
    public async Task ValidateKeyAsync_InvalidKey_ReturnsNull()
    {
        using var db = CreateDb();
        var service = new DataApiKeyService(db);

        await service.CreateKeyAsync("Test", ["site-1"]);
        var result = await service.ValidateKeyAsync("tfrg_data_invalid");

        Assert.Null(result);
    }

    [Fact]
    public async Task RegenerateKeyAsync_InvalidatesOldKey()
    {
        using var db = CreateDb();
        var service = new DataApiKeyService(db);

        var (oldKey, entity) = await service.CreateKeyAsync("Test", ["site-1"]);
        var newKey = await service.RegenerateKeyAsync(entity.Id);

        Assert.NotNull(newKey);
        Assert.NotEqual(oldKey, newKey);
        Assert.StartsWith("tfrg_data_", newKey);

        Assert.Null(await service.ValidateKeyAsync(oldKey));
        Assert.NotNull(await service.ValidateKeyAsync(newKey));
    }

    [Fact]
    public async Task RegenerateKeyAsync_NonExistentId_ReturnsNull()
    {
        using var db = CreateDb();
        var service = new DataApiKeyService(db);

        var result = await service.RegenerateKeyAsync("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateKeyAsync_ChangesNameAndSites()
    {
        using var db = CreateDb();
        var service = new DataApiKeyService(db);

        var (_, entity) = await service.CreateKeyAsync("Original", ["site-1"]);
        var updated = await service.UpdateKeyAsync(entity.Id, "Renamed", ["site-2", "site-3"]);

        Assert.True(updated);

        var keys = await service.GetAllKeysAsync();
        Assert.Single(keys);
        Assert.Equal("Renamed", keys[0].Name);
        Assert.Equal(["site-2", "site-3"], keys[0].SiteIds);
    }

    [Fact]
    public async Task DeleteKeyAsync_RemovesKey()
    {
        using var db = CreateDb();
        var service = new DataApiKeyService(db);

        var (rawKey, entity) = await service.CreateKeyAsync("Test", ["site-1"]);
        var deleted = await service.DeleteKeyAsync(entity.Id);

        Assert.True(deleted);
        Assert.Empty(await service.GetAllKeysAsync());
        Assert.Null(await service.ValidateKeyAsync(rawKey));
    }

    [Fact]
    public async Task DeleteKeyAsync_NonExistentId_ReturnsFalse()
    {
        using var db = CreateDb();
        var service = new DataApiKeyService(db);

        var result = await service.DeleteKeyAsync("nonexistent");

        Assert.False(result);
    }

    [Fact]
    public async Task GetAllKeysAsync_ReturnsOrderedByName()
    {
        using var db = CreateDb();
        var service = new DataApiKeyService(db);

        await service.CreateKeyAsync("Zebra", ["site-1"]);
        await service.CreateKeyAsync("Alpha", ["site-2"]);

        var keys = await service.GetAllKeysAsync();

        Assert.Equal(2, keys.Count);
        Assert.Equal("Alpha", keys[0].Name);
        Assert.Equal("Zebra", keys[1].Name);
    }
}
