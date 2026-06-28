using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace FactFoundry.TelemetryForge.Server.Data;

/// <summary>
/// Prepares the database schema at startup. Relational providers are migrated via
/// EF Core migrations; the in-memory provider uses <c>EnsureCreated</c>. Databases
/// originally built by <c>EnsureCreated</c> are baselined automatically on first run.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Brings the database up to the latest schema. For relational providers this applies
    /// pending migrations, first baselining a legacy <c>EnsureCreated</c> database (one whose
    /// tables exist but which has no migration history) by recording the initial migration as
    /// already applied — no DDL is run against existing objects. The in-memory provider falls
    /// back to <c>EnsureCreated</c>.
    /// </summary>
    /// <param name="db">The database context to initialize.</param>
    /// <param name="logger">Optional logger for startup diagnostics.</param>
    public static async Task InitializeAsync(TelemetryForgeDbContext db, ILogger? logger = null)
    {
        if (!db.Database.IsRelational())
        {
            await db.Database.EnsureCreatedAsync();
            return;
        }

        var historyRepository = db.GetService<IHistoryRepository>();
        var historyExists = await historyRepository.ExistsAsync();

        if (!historyExists)
        {
            var creator = db.GetService<IRelationalDatabaseCreator>();
            var hasTables = await creator.HasTablesAsync();

            if (hasTables)
            {
                var initialMigration = db.Database.GetMigrations().First();
                logger?.LogInformation(
                    "Existing database without migration history detected; baselining at {Migration}.",
                    initialMigration);

                await db.Database.ExecuteSqlRawAsync(historyRepository.GetCreateIfNotExistsScript());
                await db.Database.ExecuteSqlRawAsync(
                    historyRepository.GetInsertScript(
                        new HistoryRow(initialMigration, ProductInfo.GetVersion())));
            }
        }

        await db.Database.MigrateAsync();
    }
}
