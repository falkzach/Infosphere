using System.Text;
using Npgsql;

namespace Infosphere.Postgresql.Db;

internal static class SchemaModelGenerator
{
    private static readonly HashSet<string> IncludedSchemas =
    [
        "catalog",
        "coordination",
        "memory"
    ];

    public static async Task<int> GenerateAsync(string connectionString, string outputPath)
    {
        Directory.CreateDirectory(outputPath);
        ClearGeneratedFiles(outputPath);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var tablesAndViews = await LoadRelationsAsync(connection);
        var routines = await LoadRoutinesAsync(connection);

        foreach (var relation in tablesAndViews)
        {
            var columns = await LoadColumnsAsync(connection, relation.SchemaName, relation.ObjectName);
            WriteGeneratedFile(
                outputPath,
                relation.SchemaName,
                relation.ClassName,
                BuildRelationSource(relation, columns));
        }

        foreach (var routine in routines)
        {
            WriteGeneratedFile(
                outputPath,
                routine.SchemaName,
                routine.ClassName,
                BuildRoutineSource(routine));
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Generated {tablesAndViews.Count + routines.Count} files in {outputPath}.");
        Console.ResetColor();
        return 0;
    }

    private static void ClearGeneratedFiles(string outputPath)
    {
        foreach (var file in Directory.EnumerateFiles(outputPath, "*.gen.cs", SearchOption.AllDirectories))
        {
            File.Delete(file);
        }
    }

    private static async Task<List<DatabaseRelation>> LoadRelationsAsync(NpgsqlConnection connection)
    {
        const string sql =
            """
            SELECT table_schema, table_name, table_type
            FROM information_schema.tables
            WHERE table_schema = ANY(@schemas)
              AND table_name <> 'schema_versions'
            UNION ALL
            SELECT schemaname AS table_schema, matviewname AS table_name, 'MATERIALIZED VIEW' AS table_type
            FROM pg_matviews
            WHERE schemaname = ANY(@schemas)
            ORDER BY table_schema, table_name;
            """;

        var results = new List<DatabaseRelation>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("schemas", IncludedSchemas.ToArray());

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var schemaName = reader.GetString(0);
            var objectName = reader.GetString(1);
            var relationType = reader.GetString(2);
            results.Add(new DatabaseRelation(schemaName, objectName, relationType));
        }

        return results;
    }

    private static async Task<List<DatabaseColumn>> LoadColumnsAsync(
        NpgsqlConnection connection,
        string schemaName,
        string objectName)
    {
        const string sql =
            """
            SELECT
                column_name,
                is_nullable,
                udt_name,
                data_type
            FROM information_schema.columns
            WHERE table_schema = @schemaName
              AND table_name = @objectName
            ORDER BY ordinal_position;
            """;

        var results = new List<DatabaseColumn>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("schemaName", schemaName);
        command.Parameters.AddWithValue("objectName", objectName);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(
                new DatabaseColumn(
                    reader.GetString(0),
                    string.Equals(reader.GetString(1), "YES", StringComparison.OrdinalIgnoreCase),
                    reader.GetString(2),
                    reader.GetString(3)));
        }

        return results;
    }

    private static async Task<List<DatabaseRoutine>> LoadRoutinesAsync(NpgsqlConnection connection)
    {
        const string sql =
            """
            SELECT routine_schema, routine_name, routine_type, data_type
            FROM information_schema.routines
            WHERE routine_schema = ANY(@schemas)
            ORDER BY routine_schema, routine_name;
            """;

        var results = new List<DatabaseRoutine>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("schemas", IncludedSchemas.ToArray());

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var schemaName = reader.GetString(0);
            var routineName = reader.GetString(1);
            var routineType = reader.GetString(2);
            var returnType = reader.IsDBNull(3) ? "void" : reader.GetString(3);
            results.Add(new DatabaseRoutine(schemaName, routineName, routineType, returnType));
        }

        return results;
    }

    private static void WriteGeneratedFile(string outputPath, string schemaName, string className, string source)
    {
        var schemaDirectory = Path.Combine(outputPath, ToPascalCase(schemaName));
        Directory.CreateDirectory(schemaDirectory);

        var filePath = Path.Combine(schemaDirectory, $"{className}.gen.cs");
        File.WriteAllText(filePath, source);
    }

    private static string BuildRelationSource(DatabaseRelation relation, IReadOnlyList<DatabaseColumn> columns)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("using System.Text.Json;");
        builder.AppendLine();
        builder.AppendLine($"namespace Infosphere.Core.Generated.{ToPascalCase(relation.SchemaName)};");
        builder.AppendLine();
        builder.AppendLine($"public sealed partial record {relation.ClassName}");
        builder.AppendLine("{");
        builder.AppendLine($"    public const string Schema = \"{relation.SchemaName}\";");
        builder.AppendLine($"    public const string ObjectName = \"{relation.ObjectName}\";");
        builder.AppendLine($"    public const string RelationType = \"{relation.RelationType}\";");
        builder.AppendLine();

        foreach (var column in columns)
        {
            var clrType = MapClrType(column);
            var initializer = GetPropertyInitializer(clrType, column.IsNullable);
            builder.AppendLine($"    public {clrType} {ToPascalCase(column.ColumnName)} {{ get; init; }}{initializer}");
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string BuildRoutineSource(DatabaseRoutine routine)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine($"namespace Infosphere.Core.Generated.{ToPascalCase(routine.SchemaName)};");
        builder.AppendLine();
        builder.AppendLine($"public static partial class {routine.ClassName}");
        builder.AppendLine("{");
        builder.AppendLine($"    public const string Schema = \"{routine.SchemaName}\";");
        builder.AppendLine($"    public const string RoutineName = \"{routine.RoutineName}\";");
        builder.AppendLine($"    public const string RoutineType = \"{routine.RoutineType}\";");
        builder.AppendLine($"    public const string ReturnType = \"{routine.ReturnType}\";");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string MapClrType(DatabaseColumn column)
    {
        var clrType = column.UdtName switch
        {
            "uuid" => "Guid",
            "text" => "string",
            "varchar" => "string",
            "bpchar" => "string",
            "int4" => "int",
            "int8" => "long",
            "bool" => "bool",
            "json" or "jsonb" => "JsonDocument",
            "timestamptz" => "DateTimeOffset",
            "timestamp" => "DateTime",
            "_text" => "string[]",
            _ => column.DataType switch
            {
                "USER-DEFINED" => "string",
                _ => "string"
            }
        };

        if (clrType == "string" || clrType.EndsWith("[]", StringComparison.Ordinal))
        {
            return column.IsNullable ? $"{clrType}?" : clrType;
        }

        return column.IsNullable ? $"{clrType}?" : clrType;
    }

    private static string GetPropertyInitializer(string clrType, bool isNullable)
    {
        if (isNullable)
        {
            return string.Empty;
        }

        return clrType switch
        {
            "string" => " = default!;",
            "string[]" => " = default!;",
            "JsonDocument" => " = default!;",
            _ when !IsValueType(clrType) => " = default!;",
            _ => string.Empty
        };
    }

    private static bool IsValueType(string clrType)
    {
        return clrType is "Guid" or "int" or "long" or "bool" or "DateTimeOffset" or "DateTime";
    }

    private static string ToPascalCase(string value)
    {
        return string.Concat(
            value
                .Split(['_', '-', '.'], StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private sealed record DatabaseRelation(string SchemaName, string ObjectName, string RelationType)
    {
        public string ClassName => $"{ToPascalCase(ObjectName)}Row";
    }

    private sealed record DatabaseColumn(string ColumnName, bool IsNullable, string UdtName, string DataType);

    private sealed record DatabaseRoutine(string SchemaName, string RoutineName, string RoutineType, string ReturnType)
    {
        public string ClassName => $"{ToPascalCase(RoutineName)}Routine";
    }
}
