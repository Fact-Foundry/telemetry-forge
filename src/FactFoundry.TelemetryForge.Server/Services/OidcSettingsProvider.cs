using FactFoundry.TelemetryForge.Server.Data;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FactFoundry.TelemetryForge.Server.Services;

/// <summary>
/// Reads OIDC settings from the database and applies them to OpenIdConnectOptions.
/// Settings are read at startup and cached. A server restart is required after changing OIDC settings.
/// </summary>
public class OidcSettingsProvider : IPostConfigureOptions<OpenIdConnectOptions>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OidcSettingsProvider> _logger;

    public OidcSettingsProvider(IServiceProvider serviceProvider, ILogger<OidcSettingsProvider> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public void PostConfigure(string? name, OpenIdConnectOptions options)
    {
        if (name != "oidc")
            return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TelemetryForgeDbContext>();
            var settings = db.ServerSettings.AsNoTracking()
                .Where(s => s.Key.StartsWith("Oidc:"))
                .ToDictionary(s => s.Key, s => s.Value);

            if (!settings.TryGetValue("Oidc:Enabled", out var enabled)
                || !string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
            {
                options.ClientId = "not-configured";
                options.Authority = "https://placeholder.invalid";
                options.MetadataAddress = "https://placeholder.invalid/.well-known/openid-configuration";
                options.RequireHttpsMetadata = false;
                options.Configuration = new OpenIdConnectConfiguration();
                return;
            }

            if (settings.TryGetValue("Oidc:Authority", out var authority) && !string.IsNullOrWhiteSpace(authority))
                options.Authority = authority;

            if (settings.TryGetValue("Oidc:ClientId", out var clientId) && !string.IsNullOrWhiteSpace(clientId))
                options.ClientId = clientId;

            if (settings.TryGetValue("Oidc:ClientSecret", out var clientSecret) && !string.IsNullOrWhiteSpace(clientSecret))
                options.ClientSecret = clientSecret;

            _logger.LogInformation("OIDC configured with authority {Authority}", options.Authority);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load OIDC settings from database");
            options.ClientId = "not-configured";
            options.Configuration = new OpenIdConnectConfiguration();
        }
    }
}
