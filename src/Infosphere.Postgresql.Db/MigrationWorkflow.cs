using DbUp;
using DbUp.Engine;
using DbUp.ScriptProviders;
using Npgsql;

namespace Infosphere.Postgresql.Db;

internal static class MigrationWorkflow
{
    public static Task<int> MigrateAsync(DatabaseContext context)
    {
        return ExecuteMigrationAsync(context.ConnectionString, context.ScriptsPath);
    }

    public static async Task<int> ValidateAsync(DatabaseContext context)
    {
        var validationConnectionString = await CreateValidationDatabaseAsync(context.ConnectionString);

        try
        {
            return await ExecuteMigrationAsync(validationConnectionString, context.ScriptsPath);
        }
        finally
        {
            await DropDatabaseAsync(validationConnectionString);
        }
    }

    public static async Task<int> ValidateAndGenerateAsync(DatabaseContext context)
    {
        var validationConnectionString = await CreateValidationDatabaseAsync(context.ConnectionString);

        try
        {
            var migrationResult = await ExecuteMigrationAsync(validationConnectionString, context.ScriptsPath);
            if (migrationResult != 0)
            {
                return migrationResult;
            }

            return await SchemaModelGenerator.GenerateAsync(validationConnectionString, context.GeneratedOutputPath);
        }
        finally
        {
            await DropDatabaseAsync(validationConnectionString);
        }
    }

    private static Task<int> ExecuteMigrationAsync(string connectionString, string scriptsPath)
    {
        if (!Directory.Exists(scriptsPath))
        {
            WriteError($"Scripts path not found at {scriptsPath}");
            return Task.FromResult(-1);
        }

        EnsureDatabase.For.PostgresqlDatabase(connectionString);
        var scriptOptions = new FileSystemScriptOptions { IncludeSubDirectories = true };

        foreach (var schemaPath in Directory.GetDirectories(scriptsPath).OrderBy(path => path))
        {
            var schemaName = Path.GetFileName(schemaPath);
            if (string.IsNullOrWhiteSpace(schemaName))
            {
                continue;
            }

            WriteInfo($"Running migrations for schema '{schemaName}'...");
            EnsureSchemaExists(connectionString, schemaName);

            var upgrader = BuildUpgrader(connectionString, schemaPath, schemaName, scriptOptions);
            var result = upgrader.PerformUpgrade();

            if (!result.Successful)
            {
                WriteError(result.Error.ToString());
                return Task.FromResult(-1);
            }

            WriteSuccess($"Migrations for schema '{schemaName}' complete.");
        }

        WriteSuccess("Success!");
        return Task.FromResult(0);
    }

    private static UpgradeEngine BuildUpgrader(
        string connectionString,
        string schemaPath,
        string schemaName,
        FileSystemScriptOptions scriptOptions)
    {
        return DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsFromFileSystem(schemaPath, scriptOptions)
            .JournalToPostgresqlTable(schemaName, "schema_versions")
            .LogToConsole()
            .Build();
    }

    private static void EnsureSchemaExists(string connectionString, string schemaName)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"""CREATE SCHEMA IF NOT EXISTS "{schemaName}";""";
        command.ExecuteNonQuery();
    }

    private static async Task<string> CreateValidationDatabaseAsync(string baseConnectionString)
    {
        var baseBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString);
        var validationDatabaseName = $"{baseBuilder.Database}_validation_{Guid.NewGuid():N}";
        var validationBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = validationDatabaseName
        };

        EnsureDatabase.For.PostgresqlDatabase(validationBuilder.ConnectionString);
        WriteInfo($"Created validation database '{validationDatabaseName}'.");
        return validationBuilder.ConnectionString;
    }

    private static async Task DropDatabaseAsync(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("Validation connection string must include a database name.");
        }

        var adminBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = "postgres"
        };

        await using var connection = new NpgsqlConnection(adminBuilder.ConnectionString);
        await connection.OpenAsync();

        await using (var terminateCommand = connection.CreateCommand())
        {
            terminateCommand.CommandText =
                """
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = @databaseName
                  AND pid <> pg_backend_pid();
                """;
            terminateCommand.Parameters.AddWithValue("databaseName", databaseName);
            await terminateCommand.ExecuteNonQueryAsync();
        }

        await using (var dropCommand = connection.CreateCommand())
        {
            dropCommand.CommandText = $"""DROP DATABASE IF EXISTS "{databaseName}";""";
            await dropCommand.ExecuteNonQueryAsync();
        }

        WriteInfo($"Dropped validation database '{databaseName}'.");
    }

    private static void WriteInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private static void WriteSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}
