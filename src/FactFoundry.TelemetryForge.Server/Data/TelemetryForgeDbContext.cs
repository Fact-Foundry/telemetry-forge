using System.Text.Json;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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
    /// Configured downstream event sinks.
    /// </summary>
    public DbSet<Sink> Sinks => Set<Sink>();

    /// <summary>
    /// Raw web telemetry events (per-request).
    /// </summary>
    public DbSet<WebEvent> WebEvents => Set<WebEvent>();

    /// <summary>
    /// Materialized web telemetry sessions (computed from WebEvents).
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

    /// <summary>
    /// API keys that grant read access to telemetry data for scoped site sets.
    /// </summary>
    public DbSet<DataApiKey> DataApiKeys => Set<DataApiKey>();

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

        modelBuilder.Entity<Sink>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<WebEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SiteId).IsRequired();
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(20);
            entity.HasIndex(e => e.SiteId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.SessionHash);
            entity.HasIndex(e => e.Materialized);
            entity.HasIndex(e => e.IsBot);
        });

        modelBuilder.Entity<WebSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SiteId).IsRequired();
            entity.HasIndex(e => e.SiteId);
            entity.HasIndex(e => e.SessionStart);
            entity.Property(e => e.PagePath).HasJsonConversion();
            entity.Property(e => e.StatusCodes).HasJsonConversion();
        });

        modelBuilder.Entity<DesktopSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SiteId).IsRequired();
            entity.HasIndex(e => e.SiteId);
            entity.HasIndex(e => e.SessionStart);
            entity.HasIndex(e => e.SessionId);
            entity.Property(e => e.FeaturePath).HasJsonConversion();
            entity.Property(e => e.ErrorEvents).HasJsonConversion();
        });

        modelBuilder.Entity<MobileSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SiteId).IsRequired();
            entity.HasIndex(e => e.SiteId);
            entity.HasIndex(e => e.SessionStart);
            entity.HasIndex(e => e.SessionId);
            entity.Property(e => e.FeaturePath).HasJsonConversion();
            entity.Property(e => e.ErrorEvents).HasJsonConversion();
        });

        modelBuilder.Entity<DataApiKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ApiKeyHash).IsRequired();
            entity.Property(e => e.SiteIds).HasJsonConversion();
        });
    }
}

/// <summary>
/// Extension methods for configuring JSON value conversion on EF Core properties.
/// </summary>
internal static class ValueConversionExtensions
{
    /// <summary>
    /// Configures a property to be stored as a JSON string in the database.
    /// </summary>
    public static Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<T> HasJsonConversion<T>(
        this Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<T> builder) where T : class
    {
        var converter = new ValueConverter<T, string>(
            v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
            v => JsonSerializer.Deserialize<T>(v, JsonSerializerOptions.Default)!);

        var comparer = new ValueComparer<T>(
            (a, b) => JsonSerializer.Serialize(a, JsonSerializerOptions.Default) == JsonSerializer.Serialize(b, JsonSerializerOptions.Default),
            v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default).GetHashCode(),
            v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, JsonSerializerOptions.Default), JsonSerializerOptions.Default)!);

        builder.HasConversion(converter);
        builder.Metadata.SetValueComparer(comparer);
        return builder;
    }
}
