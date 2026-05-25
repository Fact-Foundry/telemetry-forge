using System.Threading.RateLimiting;
using FactFoundry.TelemetryForge.Server.Api;
using FactFoundry.TelemetryForge.Server.Components;
using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Database
var dbProvider = builder.Configuration.GetValue("Database:Provider", "InMemory");
builder.Services.AddDbContext<TelemetryForgeDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("TelemetryForge");

    switch (dbProvider)
    {
        case "PostgreSql":
            options.UseNpgsql(connectionString);
            break;
        case "SqlServer":
            options.UseSqlServer(connectionString);
            break;
        // MySQL support deferred until Pomelo ships a .NET 10-compatible package
        default:
            options.UseInMemoryDatabase("TelemetryForge");
            break;
    }
});

// Authentication
builder.Services.AddSingleton<IPostConfigureOptions<OpenIdConnectOptions>, OidcSettingsProvider>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    })
    .AddOpenIdConnect("oidc", options =>
    {
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.SaveTokens = false;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.CallbackPath = "/signin-oidc";
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Events = new OpenIdConnectEvents
        {
            OnTicketReceived = async context =>
            {
                var email = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                    ?? context.Principal?.FindFirst("email")?.Value
                    ?? context.Principal?.FindFirst("preferred_username")?.Value;

                if (string.IsNullOrEmpty(email))
                {
                    context.HandleResponse();
                    context.HttpContext.Response.Redirect("/login?error=oidc-noemail");
                    return;
                }

                var authService = context.HttpContext.RequestServices.GetRequiredService<AuthService>();
                var principal = await authService.AuthenticateOidcUserAsync(email);

                if (principal is null)
                {
                    context.HandleResponse();
                    context.HttpContext.Response.Redirect("/login?error=oidc-unauthorized");
                    return;
                }

                context.Principal = principal;
            },
            OnRemoteFailure = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<OpenIdConnectOptions>>();
                logger.LogError(context.Failure, "OIDC authentication failed");
                context.HandleResponse();
                context.Response.Redirect("/login?error=oidc-failed");
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("telemetry", context =>
    {
        var apiKey = context.Request.Headers["X-TelemetryForge-Key"].FirstOrDefault() ?? "unknown";
        var clientIp = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        var partitionKey = $"{apiKey}:{clientIp}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: partitionKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync("Rate limit exceeded.", cancellationToken);
    };
});

// Services
builder.Services.AddScoped<ApiKeyService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<VisitorHashService>();
builder.Services.AddSingleton<UserAgentParserService>();
builder.Services.AddSingleton<GeoLocationService>();
builder.Services.AddSingleton<LoggingEventPublisher>();
builder.Services.AddScoped<DatabaseEventPublisher>();
builder.Services.AddScoped<IEventPublisher>(sp =>
{
    var sinks = new IEventPublisher[]
    {
        sp.GetRequiredService<LoggingEventPublisher>(),
        sp.GetRequiredService<DatabaseEventPublisher>()
    };
    return new CompositeEventPublisher(sinks, sp.GetRequiredService<ILogger<CompositeEventPublisher>>());
});

// Background services
builder.Services.AddHostedService<SessionMaterializationService>();

// Blazor + MudBlazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

var app = builder.Build();

// Ensure database is created (in-memory or development)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TelemetryForgeDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// API endpoints
app.MapAuthEndpoints();
app.MapTelemetryEndpoints();
app.MapSiteEndpoints();

// Blazor
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
