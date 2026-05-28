using System.Security.Cryptography;
using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FactFoundry.TelemetryForge.Server.Services;

/// <summary>
/// Manages data API keys that grant read access to telemetry data for scoped site sets.
/// </summary>
public class DataApiKeyService
{
    private const string KeyPrefix = "tfrg_data_";
    private const int KeyByteLength = 32;

    private readonly TelemetryForgeDbContext _db;

    public DataApiKeyService(TelemetryForgeDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Creates a new data API key with the given name and site scope.
    /// Returns the raw key (shown once) and the persisted entity.
    /// </summary>
    public async Task<(string RawKey, DataApiKey Entity)> CreateKeyAsync(string name, List<string> siteIds)
    {
        var bytes = RandomNumberGenerator.GetBytes(KeyByteLength);
        var rawKey = KeyPrefix + Convert.ToHexStringLower(bytes);
        var hash = BCrypt.Net.BCrypt.HashPassword(rawKey);

        var entity = new DataApiKey
        {
            Name = name.Trim(),
            ApiKeyHash = hash,
            SiteIds = siteIds,
            CreatedAt = DateTime.UtcNow
        };

        _db.DataApiKeys.Add(entity);
        await _db.SaveChangesAsync();

        return (rawKey, entity);
    }

    /// <summary>
    /// Regenerates the API key for an existing data API key entry.
    /// Returns the new raw key (shown once).
    /// </summary>
    public async Task<string?> RegenerateKeyAsync(string id)
    {
        var entity = await _db.DataApiKeys.FindAsync(id);
        if (entity is null) return null;

        var bytes = RandomNumberGenerator.GetBytes(KeyByteLength);
        var rawKey = KeyPrefix + Convert.ToHexStringLower(bytes);
        entity.ApiKeyHash = BCrypt.Net.BCrypt.HashPassword(rawKey);
        await _db.SaveChangesAsync();

        return rawKey;
    }

    /// <summary>
    /// Updates the name and site scope of an existing data API key.
    /// </summary>
    public async Task<bool> UpdateKeyAsync(string id, string name, List<string> siteIds)
    {
        var entity = await _db.DataApiKeys.FindAsync(id);
        if (entity is null) return false;

        entity.Name = name.Trim();
        entity.SiteIds = siteIds;
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Deletes a data API key.
    /// </summary>
    public async Task<bool> DeleteKeyAsync(string id)
    {
        var entity = await _db.DataApiKeys.FindAsync(id);
        if (entity is null) return false;

        _db.DataApiKeys.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Returns all data API keys (without raw key values).
    /// </summary>
    public async Task<List<DataApiKey>> GetAllKeysAsync()
    {
        return await _db.DataApiKeys.AsNoTracking().OrderBy(k => k.Name).ToListAsync();
    }

    /// <summary>
    /// Validates a raw API key against stored hashes and returns the authorized site IDs if valid.
    /// </summary>
    public async Task<List<string>?> ValidateKeyAsync(string rawKey)
    {
        var keys = await _db.DataApiKeys.ToListAsync();
        var match = keys.FirstOrDefault(k => BCrypt.Net.BCrypt.Verify(rawKey, k.ApiKeyHash));
        return match?.SiteIds;
    }
}
