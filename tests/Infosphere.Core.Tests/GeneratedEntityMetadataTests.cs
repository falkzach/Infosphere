using Infosphere.Core.Generated.Catalog;
using Infosphere.Core.Generated.Coordination;
using Infosphere.Core.Generated.Memory;

namespace Infosphere.Core.Tests;

public sealed class GeneratedEntityMetadataTests
{
    [Fact]
    public void Generated_rows_expose_expected_catalog_metadata()
    {
        Assert.Equal("catalog", BrainProfilesRow.Schema);
        Assert.Equal("brain_profiles", BrainProfilesRow.ObjectName);
        Assert.Equal("BASE TABLE", BrainProfilesRow.RelationType);

        Assert.Equal("catalog", WorkspacesRow.Schema);
        Assert.Equal("workspaces", WorkspacesRow.ObjectName);
        Assert.Equal("BASE TABLE", WorkspacesRow.RelationType);
    }

    [Fact]
    public void Generated_rows_expose_expected_coordination_metadata()
    {
        Assert.Equal("coordination", TasksRow.Schema);
        Assert.Equal("tasks", TasksRow.ObjectName);
        Assert.Equal("coordination", AgentSessionsRow.Schema);
        Assert.Equal("agent_sessions", AgentSessionsRow.ObjectName);
        Assert.Equal("coordination", AgentSessionHeartbeatsRow.Schema);
        Assert.Equal("agent_session_heartbeats", AgentSessionHeartbeatsRow.ObjectName);
    }

    [Fact]
    public void Generated_routine_exposes_expected_metadata()
    {
        Assert.Equal("coordination", PruneAgentSessionHeartbeatsRoutine.Schema);
        Assert.Equal("prune_agent_session_heartbeats", PruneAgentSessionHeartbeatsRoutine.RoutineName);
        Assert.Equal("FUNCTION", PruneAgentSessionHeartbeatsRoutine.RoutineType);
        Assert.Equal("integer", PruneAgentSessionHeartbeatsRoutine.ReturnType);
    }

    [Fact]
    public void Generated_rows_use_expected_property_types()
    {
        var agentSession = new AgentSessionsRow();
        var task = new TasksRow();
        var contextEntry = new ContextEntriesRow();

        Assert.IsType<Guid>(agentSession.Id);
        Assert.IsType<int>(agentSession.StateId);
        Assert.Null(agentSession.CurrentTaskId);

        Assert.IsType<int>(task.StateId);
        Assert.Null(task.AssignedAgentId);

        Assert.Null(contextEntry.TaskId);
    }
}
