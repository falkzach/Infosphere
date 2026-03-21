using Microsoft.Extensions.Configuration;

namespace Infosphere.Postgresql.Db;

internal static class Cli
{
    public static async Task<int> RunAsync(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
        var runtimeDirectory = AppContext.BaseDirectory;
        var config = new ConfigurationBuilder()
            .SetBasePath(runtimeDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.User.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            WriteError("ConnectionStrings:Postgres is required.");
            return -1;
        }

        var options = CommandOptions.Parse(args);
        var generatedOutputPath = options.Command is "generate" or "sync-models"
            ? ResolveCoreGeneratedDirectory()
            : string.Empty;
        var context = new DatabaseContext(
            connectionString,
            Path.Combine(runtimeDirectory, "Scripts"),
            generatedOutputPath);

        return options.Command switch
        {
            "migrate" => await MigrationWorkflow.MigrateAsync(context),
            "validate" => await MigrationWorkflow.ValidateAsync(context),
            "generate" => await SchemaModelGenerator.GenerateAsync(connectionString, context.GeneratedOutputPath),
            "sync-models" => await MigrationWorkflow.ValidateAndGenerateAsync(context),
            _ => WriteUsage()
        };
    }

    private static string ResolveCoreGeneratedDirectory()
    {
        var repositoryRoot = FindRepositoryRoot();
        return Path.Combine(repositoryRoot, "src", "Infosphere.Core", "Generated");
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var current = new DirectoryInfo(startPath);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Infosphere.slnx")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root containing Infosphere.slnx.");
    }

    private static int WriteUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project src/Infosphere.Postgresql.Db -- [migrate|validate|generate|sync-models]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  migrate       Apply migrations to the configured database.");
        Console.WriteLine("  validate      Apply migrations to a temporary database and drop it.");
        Console.WriteLine("  generate      Generate .gen.cs database models from the configured database.");
        Console.WriteLine("  sync-models   Validate migrations in a temporary database, generate models, then drop it.");
        return -1;
    }

    private static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: {message}");
        Console.ResetColor();
    }
}
