using System.Text.RegularExpressions;
using UAParser;

namespace FactFoundry.TelemetryForge.Server.Services;

/// <summary>
/// Parses User-Agent strings and Client Hints to extract browser, OS, and device type information.
/// Prefers Client Hints when available for more accurate identification.
/// </summary>
public partial class UserAgentParserService
{
    private static readonly Parser Parser = Parser.GetDefault();

    /// <summary>
    /// Parses a User-Agent string with optional Client Hints and returns the extracted components.
    /// When Client Hints are available, they take precedence over the UA string for browser and platform.
    /// </summary>
    public UserAgentInfo Parse(string userAgent, string? secChUa = null, string? secChUaMobile = null, string? secChUaPlatform = null)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return new UserAgentInfo(null, null, null);

        var clientInfo = Parser.Parse(userAgent);

        var browser = FormatBrowser(clientInfo.UA);
        var os = FormatOs(clientInfo.OS);
        var deviceType = ClassifyDevice(clientInfo.Device, userAgent);

        if (!string.IsNullOrWhiteSpace(secChUa))
        {
            var chBrowser = ParseBrowserFromClientHints(secChUa);
            if (chBrowser is not null)
                browser = chBrowser;
        }

        if (!string.IsNullOrWhiteSpace(secChUaPlatform))
        {
            var platform = secChUaPlatform.Trim('"');
            if (!string.IsNullOrEmpty(platform) && platform != "Unknown")
                os = platform;
        }

        if (!string.IsNullOrWhiteSpace(secChUaMobile) && deviceType is not "bot" and not "tablet")
        {
            deviceType = secChUaMobile.Trim() == "?1" ? "mobile" : "desktop";
        }

        return new UserAgentInfo(browser, os, deviceType);
    }

    private static string? ParseBrowserFromClientHints(string secChUa)
    {
        var brands = BrandRegex().Matches(secChUa);
        if (brands.Count == 0)
            return null;

        string? bestBrand = null;
        string? bestVersion = null;

        foreach (Match brand in brands)
        {
            var name = brand.Groups[1].Value.Trim();
            var version = brand.Groups[2].Value.Trim();

            if (name.Contains("Not") || name.Contains("Chromium") || name.Contains("Google Chrome"))
                continue;

            bestBrand = name;
            bestVersion = version;
        }

        if (bestBrand is null)
        {
            foreach (Match brand in brands)
            {
                var name = brand.Groups[1].Value.Trim();
                var version = brand.Groups[2].Value.Trim();

                if (name.Contains("Not"))
                    continue;

                if (name == "Google Chrome")
                {
                    bestBrand = "Chrome";
                    bestVersion = version;
                    break;
                }

                if (name == "Chromium")
                {
                    bestBrand = name;
                    bestVersion = version;
                }
            }
        }

        if (bestBrand is null)
            return null;

        return string.IsNullOrEmpty(bestVersion) ? bestBrand : $"{bestBrand} {bestVersion}";
    }

    [GeneratedRegex("\"([^\"]+)\";v=\"([^\"]+)\"")]
    private static partial Regex BrandRegex();

    private static string? FormatBrowser(UserAgent ua)
    {
        if (string.IsNullOrEmpty(ua.Family) || ua.Family == "Other")
            return null;

        return string.IsNullOrEmpty(ua.Major) ? ua.Family : $"{ua.Family} {ua.Major}";
    }

    private static string? FormatOs(OS os)
    {
        if (string.IsNullOrEmpty(os.Family) || os.Family == "Other")
            return null;

        return string.IsNullOrEmpty(os.Major) ? os.Family : $"{os.Family} {os.Major}";
    }

    private static string ClassifyDevice(Device device, string userAgent)
    {
        if (IsBotUserAgent(device, userAgent))
            return "bot";

        var family = device.Family?.ToLowerInvariant() ?? string.Empty;

        if (family.Contains("spider") || family.Contains("bot") || family.Contains("crawler"))
            return "bot";

        if (device.Family is not null && device.Family != "Other")
        {
            if (family.Contains("ipad") || family.Contains("tablet") || family.Contains("kindle"))
                return "tablet";

            if (family.Contains("iphone") || family.Contains("android") || family.Contains("mobile")
                || family.Contains("phone") || family.Contains("ipod"))
                return "mobile";
        }

        var ua = userAgent.ToLowerInvariant();

        if (ua.Contains("tablet") || ua.Contains("ipad") || ua.Contains("kindle"))
            return "tablet";

        if (ua.Contains("mobile") || ua.Contains("iphone") || ua.Contains("ipod")
            || ua.Contains("android") && !ua.Contains("tablet"))
            return "mobile";

        return "desktop";
    }

    private static bool IsBotUserAgent(Device device, string userAgent)
    {
        if (device.Family is "Spider")
            return true;

        var ua = userAgent.ToLowerInvariant();
        return ua.Contains("bot") || ua.Contains("crawl") || ua.Contains("spider")
            || ua.Contains("scrape") || ua.Contains("headless")
            || ua.Contains("curl/") || ua.Contains("wget/") || ua.Contains("httpie/");
    }
}

/// <summary>
/// Parsed User-Agent components.
/// </summary>
public record UserAgentInfo(string? Browser, string? Os, string? DeviceType);
