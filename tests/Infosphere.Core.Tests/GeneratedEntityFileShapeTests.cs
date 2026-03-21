namespace Infosphere.Core.Tests;

public sealed class GeneratedEntityFileShapeTests
{
    [Fact]
    public void Generated_entity_files_exist_for_known_database_objects()
    {
        var generatedRoot = Path.Combine(GetRepositoryRoot(), "src", "Infosphere.Core", "Generated");
        var generatedFiles = Directory.GetFiles(generatedRoot, "*.gen.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetFileName(path)!)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(
            [
                "AgentSessionHeartbeatsRow.gen.cs",
                "AgentSessionsRow.gen.cs",
                "AgentSessionStatesRow.gen.cs",
                "BrainProfilesRow.gen.cs",
                "ContextEntriesRow.gen.cs",
                "PruneAgentSessionHeartbeatsRoutine.gen.cs",
                "TasksRow.gen.cs",
                "TaskStatesRow.gen.cs",
                "WorkspaceMessagesRow.gen.cs",
                "WorkspacesRow.gen.cs"
            ],
            generatedFiles);
    }

    [Fact]
    public void Generated_row_file_contains_nullable_header_and_initializer_conventions()
    {
        var filePath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "Infosphere.Core",
            "Generated",
            "Coordination",
            "AgentSessionsRow.gen.cs");

        var contents = File.ReadAllText(filePath);

        Assert.Contains("#nullable enable", contents);
        Assert.Contains("public string AgentId { get; init; } = default!;", contents);
        Assert.Contains("public Guid? CurrentTaskId { get; init; }", contents);
        Assert.Contains("public JsonDocument Metadata { get; init; } = default!;", contents);
    }

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Infosphere.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
