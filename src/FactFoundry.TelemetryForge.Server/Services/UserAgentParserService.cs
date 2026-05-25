using UAParser;

namespace FactFoundry.TelemetryForge.Server.Services;

/// <summary>
/// Parses User-Agent strings to extract browser, OS, and device type information.
/// </summary>
public class UserAgentParserService
{
    private static readonly Parser Parser = Parser.GetDefault();

    /// <summary>
    /// Parses a User-Agent string and returns the extracted components.
    /// </summary>
    public UserAgentInfo Parse(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return new UserAgentInfo(null, null, null);

        var clientInfo = Parser.Parse(userAgent);

        var browser = FormatBrowser(clientInfo.UA);
        var os = FormatOs(clientInfo.OS);
        var deviceType = ClassifyDevice(clientInfo.Device, userAgent);

        return new UserAgentInfo(browser, os, deviceType);
    }

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
            || ua.Contains("scrape") || ua.Contains("headless");
    }
}

/// <summary>
/// Parsed User-Agent components.
/// </summary>
public record UserAgentInfo(string? Browser, string? Os, string? DeviceType);
