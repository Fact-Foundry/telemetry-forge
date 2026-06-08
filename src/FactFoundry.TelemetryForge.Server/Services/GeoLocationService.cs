using System.Net;
using FactFoundry.TelemetryForge.Server.Data;
using MaxMind.GeoIP2;
using Microsoft.EntityFrameworkCore;

namespace FactFoundry.TelemetryForge.Server.Services;

/// <summary>
/// Resolves IP addresses to country and region using a MaxMind GeoLite2 database.
/// Used as a fallback when the SDK does not supply a country (e.g., no CloudFlare).
/// Returns null values gracefully when no database is configured.
/// </summary>
public class GeoLocationService : IDisposable
{
    private readonly DatabaseReader? _reader;
    private readonly ILogger<GeoLocationService> _logger;

    public GeoLocationService(IConfiguration configuration, IServiceProvider serviceProvider, ILogger<GeoLocationService> logger)
    {
        _logger = logger;

        var dbPath = configuration.GetValue<string>("GeoIP:DatabasePath");

        if (string.IsNullOrWhiteSpace(dbPath))
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TelemetryForgeDbContext>();
                var setting = db.ServerSettings.AsNoTracking()
                    .FirstOrDefault(s => s.Key == "GeoIP:DatabasePath");
                dbPath = setting?.Value;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not read GeoIP setting from database");
            }
        }

        if (!string.IsNullOrWhiteSpace(dbPath) && File.Exists(dbPath))
        {
            try
            {
                _reader = new DatabaseReader(dbPath);
                _logger.LogInformation("GeoIP database loaded from {Path}", dbPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load GeoIP database from {Path}", dbPath);
            }
        }
        else
        {
            _logger.LogInformation("GeoIP database not configured — country/region fields will rely on SDK-provided values");
        }
    }

    /// <summary>
    /// Whether a GeoIP database is loaded and available for fallback lookups.
    /// </summary>
    public bool IsDatabaseAvailable => _reader is not null;

    /// <summary>
    /// Resolves an IP address to country and region using the MaxMind database.
    /// Returns nulls if the database is not configured or the IP cannot be resolved.
    /// </summary>
    public GeoLocationResult LookupDatabase(IPAddress? ipAddress)
    {
        if (_reader is null || ipAddress is null)
            return GeoLocationResult.Empty;

        try
        {
            if (IPAddress.IsLoopback(ipAddress) || ipAddress.Equals(IPAddress.IPv6Loopback))
                return GeoLocationResult.Empty;

            if (_reader.TryCity(ipAddress, out var response) && response is not null)
            {
                return new GeoLocationResult(
                    response.Country?.Name,
                    response.Country?.IsoCode,
                    response.MostSpecificSubdivision?.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "GeoIP lookup failed for {IP}", ipAddress);
        }

        return GeoLocationResult.Empty;
    }

    /// <summary>
    /// Extracts the client IP from the request, preferring CloudFlare's
    /// <c>CF-Connecting-IP</c> header, then <c>X-Forwarded-For</c>, then the
    /// connection's remote address. This ensures the true client IP is resolved
    /// when the server sits behind a CDN/reverse proxy.
    /// </summary>
    public static IPAddress? GetClientIp(HttpContext context)
    {
        var cfConnecting = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(cfConnecting) && IPAddress.TryParse(cfConnecting.Trim(), out var cfParsed))
            return cfParsed;

        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
        {
            var firstIp = forwarded.Split(',', StringSplitOptions.TrimEntries)[0];
            if (IPAddress.TryParse(firstIp, out var parsed))
                return parsed;
        }

        return context.Connection.RemoteIpAddress;
    }

    /// <summary>
    /// Resolves an English country name from an ISO 3166-1 alpha-2 country code
    /// (e.g. "US" → "United States"). Returns null for null/invalid codes.
    /// </summary>
    public static string? CountryNameFromCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 2)
            return null;

        try
        {
            return new System.Globalization.RegionInfo(code.ToUpperInvariant()).EnglishName;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _reader?.Dispose();
    }
}

/// <summary>
/// Country, country code, and region resolved from an IP address.
/// </summary>
public record GeoLocationResult(string? Country, string? CountryCode, string? Region)
{
    /// <summary>
    /// Empty result when geolocation is unavailable or the IP cannot be resolved.
    /// </summary>
    public static readonly GeoLocationResult Empty = new(null, null, null);
}
