namespace FactFoundry.TelemetryForge.Server.Api;

/// <summary>
/// Minimal API endpoint definitions for site/app registration.
/// </summary>
public static class SiteEndpoints
{
    /// <summary>
    /// Maps site management endpoints.
    /// </summary>
    public static void MapSiteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sites")
            .RequireAuthorization();

        group.MapPost("/register", (Delegate)HandleRegister);
    }

    private static Task<IResult> HandleRegister(HttpContext context)
    {
        // TODO: Create site, generate API key, return key (shown once)
        return Task.FromResult(Results.Ok());
    }
}
