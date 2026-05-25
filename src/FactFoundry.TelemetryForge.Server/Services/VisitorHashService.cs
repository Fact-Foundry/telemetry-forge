using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FactFoundry.TelemetryForge.Server.Services;

/// <summary>
/// Resolves visitor identity by looking up hashed identifiers in the database.
/// Determines first-visit / first-install status and inserts new hashes as needed.
/// </summary>
public class VisitorHashService
{
    private readonly TelemetryForgeDbContext _db;

    public VisitorHashService(TelemetryForgeDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Checks whether the given visitor hash has been seen before for a site.
    /// If new, inserts the hash and returns true. If already known but the session hash
    /// matches the first session, returns true (still on first visit). Otherwise returns false.
    /// </summary>
    public async Task<bool> IsFirstSeenAsync(string hash, HashType hashType, SiteType sourceType, string siteId, string? sessionHash = null)
    {
        var existing = await _db.VisitorHashes
            .FirstOrDefaultAsync(v => v.Hash == hash && v.SiteId == siteId);

        if (existing is null)
        {
            _db.VisitorHashes.Add(new VisitorHash
            {
                Hash = hash,
                HashType = hashType,
                SourceType = sourceType,
                FirstSeen = DateTime.UtcNow,
                FirstSessionHash = sessionHash,
                SiteId = siteId
            });

            await _db.SaveChangesAsync();
            return true;
        }

        if (sessionHash is not null && existing.FirstSessionHash == sessionHash)
            return true;

        return false;
    }
}
