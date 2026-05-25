using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using FactFoundry.TelemetryForge.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace FactFoundry.TelemetryForge.Tests.Services;

public class AuthServiceTests
{
    private static TelemetryForgeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TelemetryForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TelemetryForgeDbContext(options);
    }

    [Fact]
    public async Task CreateAdminAsync_StoresUserWithHashedPassword()
    {
        var db = CreateDb();
        var service = new AuthService(db);

        var user = await service.CreateAdminAsync("test@example.com", "Test User", "SecurePassword123");

        var stored = await db.AdminUsers.FindAsync(user.Id);
        Assert.NotNull(stored);
        Assert.Equal("test@example.com", stored.Email);
        Assert.Equal("Test User", stored.DisplayName);
        Assert.NotEqual("SecurePassword123", stored.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("SecurePassword123", stored.PasswordHash));
    }

    [Fact]
    public async Task DeleteAdminAsync_RemovesUser()
    {
        var db = CreateDb();
        var service = new AuthService(db);
        var user = await service.CreateAdminAsync("test@example.com", "Test", "SecurePassword123");

        var result = await service.DeleteAdminAsync(user.Id);

        Assert.True(result);
        Assert.Null(await db.AdminUsers.FindAsync(user.Id));
    }

    [Fact]
    public async Task DeleteAdminAsync_ReturnsFalseForUnknownId()
    {
        var db = CreateDb();
        var service = new AuthService(db);

        var result = await service.DeleteAdminAsync("nonexistent");

        Assert.False(result);
    }

    [Fact]
    public async Task ResetPasswordAsync_UpdatesPasswordAndClearsLockout()
    {
        var db = CreateDb();
        var service = new AuthService(db);
        var user = await service.CreateAdminAsync("test@example.com", "Test", "OldPassword12345");

        user.FailedLoginAttempts = 5;
        user.LockedUntil = DateTime.UtcNow.AddMinutes(15);
        await db.SaveChangesAsync();

        var result = await service.ResetPasswordAsync(user.Id, "NewPassword12345");

        Assert.True(result);
        var updated = await db.AdminUsers.FindAsync(user.Id);
        Assert.NotNull(updated);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPassword12345", updated.PasswordHash));
        Assert.Equal(0, updated.FailedLoginAttempts);
        Assert.Null(updated.LockedUntil);
    }

    [Fact]
    public async Task GetAdminUsersAsync_ReturnsAllUsersOrdered()
    {
        var db = CreateDb();
        var service = new AuthService(db);
        await service.CreateAdminAsync("beta@example.com", "Beta", "SecurePassword123");
        await service.CreateAdminAsync("alpha@example.com", "Alpha", "SecurePassword123");

        var users = await service.GetAdminUsersAsync();

        Assert.Equal(2, users.Count);
        Assert.Equal("alpha@example.com", users[0].Email);
        Assert.Equal("beta@example.com", users[1].Email);
    }

    [Fact]
    public async Task SaveAndGetServerSetting_RoundTrips()
    {
        var db = CreateDb();
        var service = new AuthService(db);

        await service.SaveServerSettingAsync("Test:Key", "test-value");
        var result = await service.GetServerSettingAsync("Test:Key");

        Assert.Equal("test-value", result);
    }

    [Fact]
    public async Task SaveServerSettingAsync_UpdatesExistingValue()
    {
        var db = CreateDb();
        var service = new AuthService(db);

        await service.SaveServerSettingAsync("Test:Key", "original");
        await service.SaveServerSettingAsync("Test:Key", "updated");
        var result = await service.GetServerSettingAsync("Test:Key");

        Assert.Equal("updated", result);
    }

    [Fact]
    public async Task GetServerSettingAsync_ReturnsNullForMissingKey()
    {
        var db = CreateDb();
        var service = new AuthService(db);

        var result = await service.GetServerSettingAsync("Missing:Key");

        Assert.Null(result);
    }

    [Fact]
    public async Task AuthenticateAsync_SucceedsWithCorrectPassword()
    {
        var db = CreateDb();
        var service = new AuthService(db);
        await service.CreateAdminAsync("test@example.com", "Test", "SecurePassword123");

        var principal = await service.AuthenticateAsync("test@example.com", "SecurePassword123");

        Assert.NotNull(principal);
    }

    [Fact]
    public async Task AuthenticateAsync_FailsWithWrongPassword()
    {
        var db = CreateDb();
        var service = new AuthService(db);
        await service.CreateAdminAsync("test@example.com", "Test", "SecurePassword123");

        var principal = await service.AuthenticateAsync("test@example.com", "WrongPassword123");

        Assert.Null(principal);
    }

    [Fact]
    public async Task AuthenticateAsync_LocksOutAfterRepeatedFailures()
    {
        var db = CreateDb();
        var service = new AuthService(db);
        await service.CreateAdminAsync("test@example.com", "Test", "SecurePassword123");

        for (int i = 0; i < 5; i++)
            await service.AuthenticateAsync("test@example.com", "WrongPassword123");

        var principal = await service.AuthenticateAsync("test@example.com", "SecurePassword123");
        Assert.Null(principal);

        var user = await db.AdminUsers.FirstAsync(u => u.Email == "test@example.com");
        Assert.NotNull(user.LockedUntil);
        Assert.True(user.LockedUntil > DateTime.UtcNow);
    }

    [Fact]
    public async Task CreateOidcAdminAsync_StoresUserWithNoPassword()
    {
        var db = CreateDb();
        var service = new AuthService(db);

        var user = await service.CreateOidcAdminAsync("oidc@example.com", "OIDC User");

        Assert.True(user.IsOidcUser);
        Assert.Null(user.PasswordHash);
        Assert.Equal("oidc@example.com", user.Email);
    }

    [Fact]
    public async Task AuthenticateOidcUserAsync_ReturnsNullForNonOidcUser()
    {
        var db = CreateDb();
        var service = new AuthService(db);
        await service.CreateAdminAsync("local@example.com", "Local", "SecurePassword123");

        var principal = await service.AuthenticateOidcUserAsync("local@example.com");

        Assert.Null(principal);
    }

    [Fact]
    public async Task AuthenticateOidcUserAsync_ReturnsNullForUnknownEmail()
    {
        var db = CreateDb();
        var service = new AuthService(db);

        var principal = await service.AuthenticateOidcUserAsync("unknown@example.com");

        Assert.Null(principal);
    }

    [Fact]
    public async Task AuthenticateOidcUserAsync_SucceedsForAuthorizedOidcUser()
    {
        var db = CreateDb();
        var service = new AuthService(db);
        await service.CreateOidcAdminAsync("oidc@example.com", "OIDC User");

        var principal = await service.AuthenticateOidcUserAsync("oidc@example.com");

        Assert.NotNull(principal);
    }

    [Fact]
    public async Task IsOidcEnabledAsync_ReturnsFalseWhenNotConfigured()
    {
        var db = CreateDb();
        var service = new AuthService(db);

        Assert.False(await service.IsOidcEnabledAsync());
    }

    [Fact]
    public async Task IsOidcEnabledAsync_ReturnsTrueWhenFullyConfigured()
    {
        var db = CreateDb();
        var service = new AuthService(db);
        await service.SaveServerSettingAsync("Oidc:Enabled", "true");
        await service.SaveServerSettingAsync("Oidc:Authority", "https://login.example.com");
        await service.SaveServerSettingAsync("Oidc:ClientId", "client-123");

        Assert.True(await service.IsOidcEnabledAsync());
    }

    [Fact]
    public async Task IsOidcEnabledAsync_ReturnsFalseWhenEnabledButMissingFields()
    {
        var db = CreateDb();
        var service = new AuthService(db);
        await service.SaveServerSettingAsync("Oidc:Enabled", "true");

        Assert.False(await service.IsOidcEnabledAsync());
    }

    [Fact]
    public async Task AuthenticateAsync_DoesNotAuthenticateOidcUserWithPassword()
    {
        var db = CreateDb();
        var service = new AuthService(db);
        await service.CreateOidcAdminAsync("oidc@example.com", "OIDC User");

        var principal = await service.AuthenticateAsync("oidc@example.com", "anything");

        Assert.Null(principal);
    }
}
