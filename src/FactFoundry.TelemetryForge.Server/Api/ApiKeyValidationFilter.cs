using FactFoundry.TelemetryForge.Server.Services;

namespace FactFoundry.TelemetryForge.Server.Api;

/// <summary>
/// Endpoint filter that validates the X-TelemetryForge-Key header against registered site API keys.
/// On success, stores the resolved site ID in HttpContext.Items["SiteId"].
/// </summary>
public class ApiKeyValidationFilter : IEndpointFilter
{
    /// <summary>
    /// HttpContext.Items key where the resolved site ID is stored after successful validation.
    /// </summary>
    public const string SiteIdKey = "SiteId";

    private const string HeaderName = "X-TelemetryForge-Key";

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var apiKey = httpContext.Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Results.Json(new { error = "Missing API key. Include the X-TelemetryForge-Key header." }, statusCode: 401);
        }

        var apiKeyService = httpContext.RequestServices.GetRequiredService<ApiKeyService>();
        var siteId = await apiKeyService.ValidateKeyAsync(apiKey);

        if (siteId is null)
        {
            return Results.Json(new { error = "Invalid API key." }, statusCode: 401);
        }

        httpContext.Items[SiteIdKey] = siteId;
        return await next(context);
    }
}
