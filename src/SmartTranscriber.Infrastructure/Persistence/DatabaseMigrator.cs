using DbUp;

namespace SmartTranscriber.Infrastructure.Persistence;

public static class DatabaseMigrator
{
    public static void Migrate(string connectionString)
    {
        EnsureDatabase.For.PostgresqlDatabase(connectionString);

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                typeof(DatabaseMigrator).Assembly,
                name => name.StartsWith("SmartTranscriber.db.", StringComparison.Ordinal))
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            throw new InvalidOperationException("Database migration failed", result.Error);
        }
    }
}
