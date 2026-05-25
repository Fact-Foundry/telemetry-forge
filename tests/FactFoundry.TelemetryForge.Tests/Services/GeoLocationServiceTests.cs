using System.Net;
using FactFoundry.TelemetryForge.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace FactFoundry.TelemetryForge.Tests.Services;

public class GeoLocationServiceTests
{
    [Fact]
    public void GetClientIp_ReturnsXForwardedForFirst()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.50, 70.41.3.18";
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

        var result = GeoLocationService.GetClientIp(context);

        Assert.Equal(IPAddress.Parse("203.0.113.50"), result);
    }

    [Fact]
    public void GetClientIp_FallsBackToRemoteIpAddress()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.100");

        var result = GeoLocationService.GetClientIp(context);

        Assert.Equal(IPAddress.Parse("192.168.1.100"), result);
    }

    [Fact]
    public void GetClientIp_ReturnsNullWhenNoIpAvailable()
    {
        var context = new DefaultHttpContext();

        var result = GeoLocationService.GetClientIp(context);

        Assert.Null(result);
    }

    [Fact]
    public void GetClientIp_HandlesInvalidXForwardedFor()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = "not-an-ip";
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

        var result = GeoLocationService.GetClientIp(context);

        Assert.Equal(IPAddress.Parse("10.0.0.1"), result);
    }
}
