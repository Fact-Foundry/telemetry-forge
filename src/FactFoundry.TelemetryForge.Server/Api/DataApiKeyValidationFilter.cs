using FactFoundry.TelemetryForge.Server.Services;

namespace FactFoundry.TelemetryForge.Server.Api;

/// <summary>
/// Endpoint filter that validates the X-TelemetryForge-DataKey header against
/// registered data API keys. On success, stores the authorized site IDs in
/// HttpContext.Items["AuthorizedSiteIds"].
/// </summary>
public class DataApiKeyValidationFilter : IEndpointFilter
{
    /// <summary>
    /// HttpContext.Items key where the authorized site ID list is stored after validation.
    /// </summary>
    public const string AuthorizedSiteIdsKey = "AuthorizedSiteIds";

    private const string HeaderName = "X-TelemetryForge-DataKey";

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var apiKey = httpContext.Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Results.Json(new { error = "Missing API key. Include the X-TelemetryForge-DataKey header." }, statusCode: 401);
        }

        var service = httpContext.RequestServices.GetRequiredService<DataApiKeyService>();
        var siteIds = await service.ValidateKeyAsync(apiKey);

        if (siteIds is null)
        {
            return Results.Json(new { error = "Invalid API key." }, statusCode: 401);
        }

        httpContext.Items[AuthorizedSiteIdsKey] = siteIds;
        return await next(context);
    }
}
