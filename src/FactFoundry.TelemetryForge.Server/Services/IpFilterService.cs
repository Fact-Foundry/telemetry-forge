using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;

namespace FactFoundry.TelemetryForge.Server.Services;

/// <summary>
/// A single IPv4 or IPv6 address or CIDR range used for matching client IPs.
/// </summary>
public sealed class CidrRange
{
    private readonly byte[] _network;
    private readonly int _prefixBits;

    private CidrRange(byte[] network, int prefixBits)
    {
        _network = network;
        _prefixBits = prefixBits;
    }

    /// <summary>
    /// Parses a single entry — either a bare address (e.g. "203.0.113.7", "::1") treated as a
    /// host route, or CIDR notation (e.g. "203.0.113.0/24", "2001:db8::/32"). Host bits beyond
    /// the prefix are masked off, so "203.0.113.7/24" is accepted and treated as 203.0.113.0/24.
    /// </summary>
    /// <param name="entry">The address or CIDR string to parse.</param>
    /// <param name="range">The parsed range, or null when parsing fails.</param>
    /// <returns>True when the entry was parsed successfully.</returns>
    public static bool TryParse(string? entry, out CidrRange? range)
    {
        range = null;
        if (string.IsNullOrWhiteSpace(entry))
            return false;

        entry = entry.Trim();
        string addrPart;
        int prefix;
        var slash = entry.IndexOf('/');
        if (slash >= 0)
        {
            addrPart = entry[..slash];
            if (!int.TryParse(entry[(slash + 1)..], out prefix))
                return false;
        }
        else
        {
            addrPart = entry;
            prefix = -1;
        }

        if (!IPAddress.TryParse(addrPart, out var addr))
            return false;

        if (addr.IsIPv4MappedToIPv6)
            addr = addr.MapToIPv4();

        var bytes = addr.GetAddressBytes();
        var maxBits = bytes.Length * 8;
        if (prefix < 0)
            prefix = maxBits;
        if (prefix > maxBits)
            return false;

        MaskInPlace(bytes, prefix);
        range = new CidrRange(bytes, prefix);
        return true;
    }

    /// <summary>
    /// Determines whether the given address falls within this range.
    /// </summary>
    /// <param name="address">The address to test.</param>
    /// <returns>True when the address is contained in the range.</returns>
    public bool Contains(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        var bytes = address.GetAddressBytes();
        if (bytes.Length != _network.Length)
            return false; // different address family

        var fullBytes = _prefixBits / 8;
        for (var i = 0; i < fullBytes; i++)
        {
            if (_network[i] != bytes[i])
                return false;
        }

        var remBits = _prefixBits % 8;
        if (remBits > 0)
        {
            var mask = (byte)(0xFF << (8 - remBits));
            if ((_network[fullBytes] & mask) != (bytes[fullBytes] & mask))
                return false;
        }

        return true;
    }

    private static void MaskInPlace(byte[] bytes, int prefixBits)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            var bitsLeft = prefixBits - (i * 8);
            if (bitsLeft >= 8)
                continue;
            if (bitsLeft <= 0)
            {
                bytes[i] = 0;
                continue;
            }

            bytes[i] &= (byte)(0xFF << (8 - bitsLeft));
        }
    }
}

/// <summary>
/// Holds the admin-configured list of IP addresses / CIDR ranges whose web traffic should be
/// ignored (dropped at ingestion and never persisted), so a developer's own traffic does not
/// inflate analytics. The parsed rule set is cached in memory and refreshed when the setting
/// is saved.
/// </summary>
public sealed class IpFilterService
{
    /// <summary>
    /// The server-setting key under which the newline/comma-separated rule list is stored.
    /// </summary>
    public const string SettingKey = "Filter:IgnoredIps";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IpFilterService> _logger;
    private volatile IReadOnlyList<CidrRange> _ranges = Array.Empty<CidrRange>();

    /// <summary>
    /// Creates the service. Call <see cref="RefreshAsync"/> once at startup to load existing rules.
    /// </summary>
    /// <param name="scopeFactory">Scope factory used to resolve scoped data services for loading rules.</param>
    /// <param name="logger">Logger for malformed-entry and load diagnostics.</param>
    public IpFilterService(IServiceScopeFactory scopeFactory, ILogger<IpFilterService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Whether any ignore rules are currently configured.
    /// </summary>
    public bool HasRules => _ranges.Count > 0;

    /// <summary>
    /// Reloads the cached rule set from the persisted setting. Failures are logged and leave the
    /// previously loaded rules in place, so a transient data error never blocks ingestion or startup.
    /// </summary>
    public async Task RefreshAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
            var raw = await auth.GetServerSettingAsync(SettingKey);
            _ranges = Parse(raw, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load ignored-IP filter rules; keeping previously loaded rules.");
        }
    }

    /// <summary>
    /// Returns true when the given client IP matches any configured ignore rule.
    /// </summary>
    /// <param name="ip">The client IP string from the telemetry payload (may be null/empty).</param>
    public bool IsIgnored(string? ip) => Matches(ip, _ranges);

    /// <summary>
    /// Pure matching helper: returns true when <paramref name="ip"/> parses and falls within any range.
    /// </summary>
    /// <param name="ip">The candidate IP string.</param>
    /// <param name="ranges">The rule set to test against.</param>
    public static bool Matches(string? ip, IReadOnlyList<CidrRange> ranges)
    {
        if (ranges.Count == 0 || string.IsNullOrWhiteSpace(ip))
            return false;
        if (!IPAddress.TryParse(ip.Trim(), out var addr))
            return false;

        foreach (var range in ranges)
        {
            if (range.Contains(addr))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Parses a raw rule list (newline- or comma-separated; blank lines and "#" comments ignored)
    /// into matchable ranges. Malformed entries are skipped and logged.
    /// </summary>
    /// <param name="raw">The stored rule list, or null.</param>
    /// <param name="logger">Optional logger for malformed-entry warnings.</param>
    public static IReadOnlyList<CidrRange> Parse(string? raw, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<CidrRange>();

        var ranges = new List<CidrRange>();
        var entries = raw.Split(
            ['\n', '\r', ','],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var entry in entries)
        {
            if (entry.StartsWith('#'))
                continue;
            if (CidrRange.TryParse(entry, out var range) && range is not null)
                ranges.Add(range);
            else
                logger?.LogWarning("Ignoring malformed IP filter entry: {Entry}", entry);
        }

        return ranges;
    }
}
