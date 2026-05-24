using System.Threading.RateLimiting;
using FactFoundry.TelemetryForge.Server.Api;
using FactFoundry.TelemetryForge.Server.Components;
using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
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
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("telemetry", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Request.Headers["X-TelemetryForge-Key"].FirstOrDefault() ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync("Rate limit exceeded.", cancellationToken);
    };
});

// Services
builder.Services.AddScoped<ApiKeyService>();
builder.Services.AddScoped<AuthService>();

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
app.MapTelemetryEndpoints();
app.MapSiteEndpoints();

// Blazor
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
