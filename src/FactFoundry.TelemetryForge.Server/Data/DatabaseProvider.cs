namespace FactFoundry.TelemetryForge.Server.Data;

/// <summary>
/// Supported database providers for TelemetryForge Server.
/// </summary>
public enum DatabaseProvider
{
    /// <summary>In-memory database for development and testing.</summary>
    InMemory,

    /// <summary>PostgreSQL.</summary>
    PostgreSql,

    /// <summary>Microsoft SQL Server.</summary>
    SqlServer,

    /// <summary>MySQL or MariaDB.</summary>
    MySql
}
