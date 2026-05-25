using System.Net;
using FactFoundry.TelemetryForge.Server.Data;
using MaxMind.GeoIP2;
using Microsoft.EntityFrameworkCore;

namespace FactFoundry.TelemetryForge.Server.Services;

/// <summary>
/// Resolves IP addresses to country and region using a MaxMind GeoLite2 database.
/// Returns null values gracefully when no database is configured.
/// Checks IConfiguration first, then falls back to the DB-stored setting.
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
            _logger.LogInformation("GeoIP database not configured — country/region fields will be null");
        }
    }

    /// <summary>
    /// Whether a GeoIP database is loaded and available for lookups.
    /// </summary>
    public bool IsAvailable => _reader is not null;

    /// <summary>
    /// Resolves an IP address to country and region. Returns nulls if the database
    /// is not configured or the IP cannot be resolved.
    /// </summary>
    public GeoLocationResult Lookup(IPAddress? ipAddress)
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
    /// Extracts the client IP from the request, checking X-Forwarded-For first.
    /// </summary>
    public static IPAddress? GetClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
        {
            var firstIp = forwarded.Split(',', StringSplitOptions.TrimEntries)[0];
            if (IPAddress.TryParse(firstIp, out var parsed))
                return parsed;
        }

        return context.Connection.RemoteIpAddress;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _reader?.Dispose();
    }
}

/// <summary>
/// Country and region resolved from an IP address.
/// </summary>
public record GeoLocationResult(string? Country, string? Region)
{
    /// <summary>
    /// Empty result when geolocation is unavailable or the IP cannot be resolved.
    /// </summary>
    public static readonly GeoLocationResult Empty = new(null, null);
}
