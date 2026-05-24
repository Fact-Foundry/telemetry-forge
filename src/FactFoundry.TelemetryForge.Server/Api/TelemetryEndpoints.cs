namespace FactFoundry.TelemetryForge.Server.Api;

/// <summary>
/// Minimal API endpoint definitions for telemetry ingestion.
/// </summary>
public static class TelemetryEndpoints
{
    /// <summary>
    /// Maps all telemetry ingestion endpoints.
    /// </summary>
    public static void MapTelemetryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/telemetry")
            .RequireRateLimiting("telemetry");

        group.MapPost("/web", (Delegate)HandleWebPayload);
        group.MapPost("/desktop", (Delegate)HandleDesktopPayload);
        group.MapPost("/mobile", (Delegate)HandleMobilePayload);
    }

    private static Task<IResult> HandleWebPayload(HttpContext context)
    {
        // TODO: Validate API key, deserialize payload, enrich, publish
        return Task.FromResult(Results.Accepted());
    }

    private static Task<IResult> HandleDesktopPayload(HttpContext context)
    {
        // TODO: Validate API key, deserialize payload, enrich, publish
        return Task.FromResult(Results.Accepted());
    }

    private static Task<IResult> HandleMobilePayload(HttpContext context)
    {
        // TODO: Validate API key, deserialize payload, enrich, publish
        return Task.FromResult(Results.Accepted());
    }
}
