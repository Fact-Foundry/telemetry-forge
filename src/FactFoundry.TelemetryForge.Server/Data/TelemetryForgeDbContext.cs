using FactFoundry.TelemetryForge.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FactFoundry.TelemetryForge.Server.Data;

/// <summary>
/// EF Core database context for TelemetryForge Server.
/// </summary>
public class TelemetryForgeDbContext : DbContext
{
    /// <summary>
    /// Registered sites and applications.
    /// </summary>
    public DbSet<Site> Sites => Set<Site>();

    /// <summary>
    /// Hashed visitor identifiers for first-visit detection.
    /// </summary>
    public DbSet<VisitorHash> VisitorHashes => Set<VisitorHash>();

    /// <summary>
    /// Admin UI user accounts.
    /// </summary>
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    /// <summary>
    /// Server configuration settings managed from the admin UI.
    /// </summary>
    public DbSet<ServerSetting> ServerSettings => Set<ServerSetting>();

    /// <summary>
    /// Stored web telemetry sessions.
    /// </summary>
    public DbSet<WebSession> WebSessions => Set<WebSession>();

    /// <summary>
    /// Stored desktop telemetry sessions.
    /// </summary>
    public DbSet<DesktopSession> DesktopSessions => Set<DesktopSession>();

    /// <summary>
    /// Stored mobile telemetry sessions.
    /// </summary>
    public DbSet<MobileSession> MobileSessions => Set<MobileSession>();

    public TelemetryForgeDbContext(DbContextOptions<TelemetryForgeDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Site>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ApiKeyHash).IsRequired();
        });

        modelBuilder.Entity<VisitorHash>(entity =>
        {
            entity.HasKey(e => new { e.Hash, e.SiteId });
            entity.Property(e => e.Hash).IsRequired().HasMaxLength(64);
            entity.Property(e => e.SiteId).IsRequired();
            entity.HasIndex(e => new { e.SiteId, e.HashType });
        });

        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<ServerSetting>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Value).IsRequired();
        });

        modelBuilder.Entity<WebSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SiteId).IsRequired();
            entity.HasIndex(e => e.SiteId);
            entity.HasIndex(e => e.SessionStart);
        });

        modelBuilder.Entity<DesktopSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SiteId).IsRequired();
            entity.HasIndex(e => e.SiteId);
            entity.HasIndex(e => e.SessionStart);
        });

        modelBuilder.Entity<MobileSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SiteId).IsRequired();
            entity.HasIndex(e => e.SiteId);
            entity.HasIndex(e => e.SessionStart);
        });
    }
}
