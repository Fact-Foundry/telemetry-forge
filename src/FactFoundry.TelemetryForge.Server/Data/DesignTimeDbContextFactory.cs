using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FactFoundry.TelemetryForge.Server.Data;

/// <summary>
/// Design-time factory used by the <c>dotnet ef</c> CLI to author migrations.
/// Always targets PostgreSQL (the supported relational provider); the connection
/// string is a placeholder because no database is contacted when generating migrations.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TelemetryForgeDbContext>
{
    /// <summary>
    /// Creates a <see cref="TelemetryForgeDbContext"/> configured for the Npgsql provider.
    /// </summary>
    /// <param name="args">Command-line arguments passed by the EF tooling (unused).</param>
    public TelemetryForgeDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TelemetryForgeDbContext>()
            .UseNpgsql("Host=localhost;Database=telemetryforge_design;Username=design;Password=design")
            .Options;

        return new TelemetryForgeDbContext(options);
    }
}
