namespace Infosphere.Postgresql.Db;

internal sealed record DatabaseContext(
    string ConnectionString,
    string ScriptsPath,
    string GeneratedOutputPath);
