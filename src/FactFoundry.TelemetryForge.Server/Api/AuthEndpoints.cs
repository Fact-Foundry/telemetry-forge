using FactFoundry.TelemetryForge.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FactFoundry.TelemetryForge.Server.Api;

/// <summary>
/// Minimal API endpoints for authentication (login/logout).
/// </summary>
public static class AuthEndpoints
{
    /// <summary>
    /// Maps authentication-related endpoints.
    /// </summary>
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/login", async (HttpContext context, AuthService authService) =>
        {
            var form = await context.Request.ReadFormAsync();
            var email = form["email"].ToString();
            var password = form["password"].ToString();

            var principal = await authService.AuthenticateAsync(email, password);
            if (principal is null)
                return Results.Redirect("/login?error=invalid");

            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true });

            return Results.Redirect("/");
        });

        app.MapGet("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/login");
        });
    }
}
