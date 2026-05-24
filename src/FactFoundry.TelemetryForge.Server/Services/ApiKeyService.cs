using System.Security.Cryptography;
using FactFoundry.TelemetryForge.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace FactFoundry.TelemetryForge.Server.Services;

/// <summary>
/// Generates and validates API keys for registered sites and applications.
/// </summary>
public class ApiKeyService
{
    private const string KeyPrefix = "tfrg_live_";
    private const int KeyByteLength = 32;

    private readonly TelemetryForgeDbContext _db;

    public ApiKeyService(TelemetryForgeDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Generates a new cryptographically secure API key.
    /// Returns the raw key (to be shown once to the user) and its bcrypt hash (to be stored).
    /// </summary>
    public (string RawKey, string Hash) GenerateKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(KeyByteLength);
        var rawKey = KeyPrefix + Convert.ToHexStringLower(bytes);
        var hash = BCrypt.Net.BCrypt.HashPassword(rawKey);
        return (rawKey, hash);
    }

    /// <summary>
    /// Validates an API key against stored hashes and returns the associated site ID if valid.
    /// </summary>
    public async Task<string?> ValidateKeyAsync(string rawKey)
    {
        var sites = await _db.Sites.ToListAsync();
        var site = sites.FirstOrDefault(s => BCrypt.Net.BCrypt.Verify(rawKey, s.ApiKeyHash));
        return site?.Id;
    }
}
