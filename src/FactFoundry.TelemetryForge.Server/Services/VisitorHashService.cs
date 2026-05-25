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
    /// If new, inserts the hash and returns true. If already known, returns false.
    /// </summary>
    public async Task<bool> IsFirstSeenAsync(string hash, HashType hashType, SiteType sourceType, string siteId)
    {
        var exists = await _db.VisitorHashes
            .AnyAsync(v => v.Hash == hash && v.SiteId == siteId);

        if (exists)
            return false;

        _db.VisitorHashes.Add(new VisitorHash
        {
            Hash = hash,
            HashType = hashType,
            SourceType = sourceType,
            FirstSeen = DateTime.UtcNow,
            SiteId = siteId
        });

        await _db.SaveChangesAsync();
        return true;
    }
}
