using FactFoundry.TelemetryForge.Server.Services;

namespace FactFoundry.TelemetryForge.Tests.Services;

public class UserAgentParserServiceTests
{
    private readonly UserAgentParserService _parser = new();

    [Fact]
    public void Parse_ChromeOnWindows_ExtractsBrowserAndOs()
    {
        var result = _parser.Parse("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");

        Assert.StartsWith("Chrome", result.Browser);
        Assert.StartsWith("Windows", result.Os);
        Assert.Equal("desktop", result.DeviceType);
    }

    [Fact]
    public void Parse_SafariOnIPhone_ReturnsMobile()
    {
        var result = _parser.Parse("Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1");

        Assert.NotNull(result.Browser);
        Assert.NotNull(result.Os);
        Assert.Equal("mobile", result.DeviceType);
    }

    [Fact]
    public void Parse_IPad_ReturnsTablet()
    {
        var result = _parser.Parse("Mozilla/5.0 (iPad; CPU OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1");

        Assert.Equal("tablet", result.DeviceType);
    }

    [Fact]
    public void Parse_Googlebot_ReturnsBot()
    {
        var result = _parser.Parse("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)");

        Assert.Equal("bot", result.DeviceType);
    }

    [Fact]
    public void Parse_FirefoxOnLinux_ExtractsBrowserAndOs()
    {
        var result = _parser.Parse("Mozilla/5.0 (X11; Linux x86_64; rv:126.0) Gecko/20100101 Firefox/126.0");

        Assert.StartsWith("Firefox", result.Browser);
        Assert.StartsWith("Linux", result.Os);
        Assert.Equal("desktop", result.DeviceType);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsNulls()
    {
        var result = _parser.Parse("");

        Assert.Null(result.Browser);
        Assert.Null(result.Os);
        Assert.Null(result.DeviceType);
    }

    [Fact]
    public void Parse_NullString_ReturnsNulls()
    {
        var result = _parser.Parse(null!);

        Assert.Null(result.Browser);
        Assert.Null(result.Os);
        Assert.Null(result.DeviceType);
    }

    [Fact]
    public void Parse_AndroidMobile_ReturnsMobile()
    {
        var result = _parser.Parse("Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Mobile Safari/537.36");

        Assert.Equal("mobile", result.DeviceType);
    }
}
