using FactFoundry.TelemetryForge.Server.Services;

namespace FactFoundry.TelemetryForge.Tests.Services;

public class IpFilterServiceTests
{
    [Fact]
    public void Parse_NullOrBlank_ReturnsEmpty()
    {
        Assert.Empty(IpFilterService.Parse(null));
        Assert.Empty(IpFilterService.Parse(""));
        Assert.Empty(IpFilterService.Parse("   \n  \n "));
    }

    [Fact]
    public void Parse_SplitsOnNewlinesAndCommas_AndSkipsCommentsAndBlanks()
    {
        var ranges = IpFilterService.Parse("203.0.113.7\n# a comment\n\n198.51.100.0/24, 10.0.0.1");
        Assert.Equal(3, ranges.Count);
    }

    [Fact]
    public void Parse_SkipsMalformedEntries()
    {
        var ranges = IpFilterService.Parse("not-an-ip\n203.0.113.7\n999.999.0.0/8\n10.0.0.0/40");
        Assert.Single(ranges); // only 203.0.113.7 is valid
    }

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7", true)]
    [InlineData("203.0.113.7", "203.0.113.8", false)]
    public void Matches_ExactIpv4(string rule, string candidate, bool expected)
    {
        var ranges = IpFilterService.Parse(rule);
        Assert.Equal(expected, IpFilterService.Matches(candidate, ranges));
    }

    [Theory]
    [InlineData("203.0.113.0/24", "203.0.113.0", true)]
    [InlineData("203.0.113.0/24", "203.0.113.255", true)]
    [InlineData("203.0.113.0/24", "203.0.114.0", false)]
    [InlineData("10.0.0.0/8", "10.255.255.255", true)]
    [InlineData("10.0.0.0/8", "11.0.0.1", false)]
    public void Matches_Ipv4Cidr(string rule, string candidate, bool expected)
    {
        var ranges = IpFilterService.Parse(rule);
        Assert.Equal(expected, IpFilterService.Matches(candidate, ranges));
    }

    [Fact]
    public void Parse_LenientHostBits_TreatedAsNetwork()
    {
        // 203.0.113.7/24 should behave like 203.0.113.0/24
        var ranges = IpFilterService.Parse("203.0.113.7/24");
        Assert.True(IpFilterService.Matches("203.0.113.200", ranges));
        Assert.False(IpFilterService.Matches("203.0.114.1", ranges));
    }

    [Theory]
    [InlineData("2001:db8::1", "2001:db8::1", true)]
    [InlineData("2001:db8::1", "2001:db8::2", false)]
    [InlineData("2001:db8::/32", "2001:db8:abcd::1", true)]
    [InlineData("2001:db8::/32", "2001:db9::1", false)]
    public void Matches_Ipv6(string rule, string candidate, bool expected)
    {
        var ranges = IpFilterService.Parse(rule);
        Assert.Equal(expected, IpFilterService.Matches(candidate, ranges));
    }

    [Fact]
    public void Matches_Ipv4MappedIpv6_NormalizesToIpv4Rule()
    {
        // A proxy may present the client as an IPv4-mapped IPv6 address.
        var ranges = IpFilterService.Parse("203.0.113.7");
        Assert.True(IpFilterService.Matches("::ffff:203.0.113.7", ranges));
    }

    [Fact]
    public void Matches_DifferentFamily_DoesNotMatch()
    {
        var ranges = IpFilterService.Parse("203.0.113.0/24");
        Assert.False(IpFilterService.Matches("2001:db8::1", ranges));
    }

    [Fact]
    public void Matches_EmptyRulesOrBlankIp_ReturnsFalse()
    {
        Assert.False(IpFilterService.Matches("203.0.113.7", IpFilterService.Parse(null)));
        Assert.False(IpFilterService.Matches(null, IpFilterService.Parse("203.0.113.7")));
        Assert.False(IpFilterService.Matches("", IpFilterService.Parse("203.0.113.7")));
        Assert.False(IpFilterService.Matches("not-an-ip", IpFilterService.Parse("203.0.113.7")));
    }
}
