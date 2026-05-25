namespace FactFoundry.TelemetryForge.Server.Data.Entities;

/// <summary>
/// A hashed visitor identifier used solely to determine first-visit/first-install status.
/// Contains no behavioral data.
/// </summary>
public class VisitorHash
{
    /// <summary>
    /// SHA-256 hash of the visitor identifier (IP, _ga value, machine fingerprint, or device ID).
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// The type of identifier that was hashed.
    /// </summary>
    public HashType HashType { get; set; }

    /// <summary>
    /// The source platform type.
    /// </summary>
    public SiteType SourceType { get; set; }

    /// <summary>
    /// When this visitor was first seen.
    /// </summary>
    public DateTime FirstSeen { get; set; }

    /// <summary>
    /// The site this visitor hash is associated with.
    /// </summary>
    public string SiteId { get; set; } = string.Empty;
}

/// <summary>
/// The type of identifier that was hashed for visitor tracking.
/// </summary>
public enum HashType
{
    /// <summary>Hashed IP address (web).</summary>
    Ip,

    /// <summary>Hashed Google Analytics _ga cookie value (web).</summary>
    Ga,

    /// <summary>Hashed machine fingerprint (desktop).</summary>
    Fingerprint,

    /// <summary>Hashed iOS vendor ID (mobile).</summary>
    VendorId,

    /// <summary>Hashed Android ID (mobile).</summary>
    AndroidId,

    /// <summary>Hashed generated GUID fallback (mobile).</summary>
    GeneratedGuid
}
