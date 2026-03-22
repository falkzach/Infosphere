using System.Text.Json;
using Infosphere.Api.Dtos.V0;
using Npgsql;

namespace Infosphere.Api.Data;

public sealed class InfosphereRepository(NpgsqlDataSource dataSource)
{
    private static readonly JsonDocument EmptyJson = JsonDocument.Parse("{}");
    private static readonly Guid DefaultBrainProfileId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly IReadOnlyDictionary<int, TaskStateDto> TaskStates = new Dictionary<int, TaskStateDto>
    {
        [0] = new(0, "created", "Created"),
        [1] = new(1, "ready", "Ready"),
        [2] = new(2, "in_progress", "In Progress"),
        [3] = new(3, "blocked", "Blocked"),
        [4] = new(4, "completed", "Completed"),
        [5] = new(5, "cancelled", "Cancelled"),
        [99] = new(99, "error", "Error")
    };
    private static readonly IReadOnlyDictionary<int, AgentSessionStateDto> AgentSessionStates = new Dictionary<int, AgentSessionStateDto>
    {
        [0] = new(0, "created", "Created"),
        [1] = new(1, "active", "Active"),
        [2] = new(2, "idle", "Idle"),
        [3] = new(3, "blocked", "Blocked"),
        [4] = new(4, "closing", "Closing"),
        [5] = new(5, "closed", "Closed"),
        [99] = new(99, "error", "Error")
    };

    public async Task<BrainProfileDto> GetDefaultBrainProfileAsync(CancellationToken cancellationToken)
    {
        await EnsureDefaultBrainProfileAsync(cancellationToken);

        const string sql =
            """
            SELECT id, name, description, metadata, created_utc, updated_utc
            FROM catalog.brain_profiles
            WHERE id = @id;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", DefaultBrainProfileId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return new BrainProfileDto(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetFieldValue<JsonDocument>(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetFieldValue<DateTimeOffset>(5));
    }

    public async Task<IReadOnlyList<WorkspaceDto>> ListWorkspacesAsync(CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT id, brain_profile_id, key, name, description, metadata, created_utc, updated_utc
            FROM catalog.workspaces
            ORDER BY created_utc DESC;
            """;

        var results = new List<WorkspaceDto>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapWorkspace(reader));
        }

        return results;
    }

    public async Task<WorkspaceDto> CreateWorkspaceAsync(
        string key,
        string name,
        string description,
        CancellationToken cancellationToken)
    {
        await EnsureDefaultBrainProfileAsync(cancellationToken);

        const string sql =
            """
            INSERT INTO catalog.workspaces (
                id,
                brain_profile_id,
                key,
                name,
                description,
                metadata)
            VALUES (
                @id,
                @brainProfileId,
                @key,
                @name,
                @description,
                @metadata)
            RETURNING id, brain_profile_id, key, name, description, metadata, created_utc, updated_utc;
            """;

        var id = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("brainProfileId", DefaultBrainProfileId);
        command.Parameters.AddWithValue("key", key);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("description", description);
        command.Parameters.AddWithValue("metadata", EmptyJson);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return MapWorkspace(reader);
    }

    public async Task<IReadOnlyList<TaskDto>> ListTasksAsync(Guid workspaceId, bool availableOnly, CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT
                t.id,
                t.workspace_id,
                t.title,
                t.state_id,
                t.assigned_agent_id,
                t.priority,
                t.context_entry_id,
                t.created_utc,
                t.updated_utc,
                ts.key,
                ts.name
            FROM coordination.tasks t
            JOIN coordination.task_states ts ON ts.id = t.state_id
            WHERE t.workspace_id = @workspaceId
              AND (@availableOnly = FALSE OR (t.state_id = 1 AND t.assigned_agent_id IS NULL))
            ORDER BY t.priority DESC, t.created_utc DESC;
            """;

        var results = new List<TaskDto>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspaceId", workspaceId);
        command.Parameters.AddWithValue("availableOnly", availableOnly);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(
                MapTask(
                    reader,
                    new TaskStateDto(
                        reader.GetInt32(3),
                        reader.GetString(9),
                        reader.GetString(10))));
        }

        return results;
    }

    public async Task<TaskDto> CreateTaskAsync(
        Guid workspaceId,
        string title,
        int priority,
        IReadOnlyList<string>? successCriteria,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO coordination.tasks (
                id,
                workspace_id,
                title,
                state_id,
                priority)
            VALUES (
                @id,
                @workspaceId,
                @title,
                1,
                @priority)
            RETURNING id, workspace_id, title, state_id, assigned_agent_id, priority, context_entry_id, created_utc, updated_utc;
            """;

        var id = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        TaskDto task;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("workspaceId", workspaceId);
            command.Parameters.AddWithValue("title", title);
            command.Parameters.AddWithValue("priority", priority);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            task = MapTask(reader, GetTaskState(1));
        }

        if (successCriteria is { Count: > 0 })
        {
            const string checklistSql =
                """
                INSERT INTO coordination.task_checklist_items (
                    id,
                    task_id,
                    ordinal,
                    title,
                    is_required)
                VALUES (
                    @id,
                    @taskId,
                    @ordinal,
                    @title,
                    TRUE);
                """;

            for (var index = 0; index < successCriteria.Count; index++)
            {
                await using var checklistCommand = new NpgsqlCommand(checklistSql, connection, transaction);
                checklistCommand.Parameters.AddWithValue("id", Guid.NewGuid());
                checklistCommand.Parameters.AddWithValue("taskId", id);
                checklistCommand.Parameters.AddWithValue("ordinal", index + 1);
                checklistCommand.Parameters.AddWithValue("title", successCriteria[index]);
                await checklistCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return task;
    }

    public async Task<TaskDto?> ClaimTaskAsync(Guid taskId, Guid sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string taskSql =
            """
            UPDATE coordination.tasks t
            SET assigned_agent_id = s.agent_id,
                state_id = 2,
                updated_utc = NOW()
            FROM coordination.agent_sessions s
            WHERE t.id = @taskId
              AND s.id = @sessionId
              AND s.workspace_id = t.workspace_id
              AND s.state_id = 1
              AND s.current_task_id IS NULL
              AND t.state_id = 1
              AND t.assigned_agent_id IS NULL
            RETURNING t.id, t.workspace_id, t.title, t.state_id, t.assigned_agent_id, t.priority, t.context_entry_id, t.created_utc, t.updated_utc;
            """;

        TaskDto? task = null;
        await using (var taskCommand = new NpgsqlCommand(taskSql, connection, transaction))
        {
            taskCommand.Parameters.AddWithValue("taskId", taskId);
            taskCommand.Parameters.AddWithValue("sessionId", sessionId);

            await using var reader = await taskCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                task = MapTask(reader, GetTaskState(2));
            }
        }

        if (task is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        const string sessionSql =
            """
            UPDATE coordination.agent_sessions
            SET current_task_id = @taskId,
                heartbeat_utc = NOW()
            WHERE id = @sessionId;
            """;

        await using (var sessionCommand = new NpgsqlCommand(sessionSql, connection, transaction))
        {
            sessionCommand.Parameters.AddWithValue("taskId", taskId);
            sessionCommand.Parameters.AddWithValue("sessionId", sessionId);
            await sessionCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return task;
    }

    public async Task<TaskDto?> TransitionTaskAsync(
        Guid taskId,
        int stateId,
        Guid? sessionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var clearsAssignment = stateId is 1 or 4 or 5 or 99;
        const string sql =
            """
            UPDATE coordination.tasks t
            SET state_id = @stateId,
                assigned_agent_id = CASE
                    WHEN @clearAssignment THEN NULL
                    WHEN @sessionId IS NOT NULL THEN (
                        SELECT s.agent_id
                        FROM coordination.agent_sessions s
                        WHERE s.id = @sessionId
                          AND s.workspace_id = t.workspace_id)
                    ELSE t.assigned_agent_id
                END,
                updated_utc = NOW()
            WHERE t.id = @taskId
              AND (
                  @sessionId IS NULL
                  OR EXISTS (
                      SELECT 1
                      FROM coordination.agent_sessions s
                      WHERE s.id = @sessionId
                        AND s.workspace_id = t.workspace_id))
            RETURNING t.id, t.workspace_id, t.title, t.state_id, t.assigned_agent_id, t.priority, t.context_entry_id, t.created_utc, t.updated_utc;
            """;

        TaskDto? task = null;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("taskId", taskId);
            command.Parameters.AddWithValue("stateId", stateId);
            command.Parameters.Add(new NpgsqlParameter<Guid?>("sessionId", NpgsqlTypes.NpgsqlDbType.Uuid) { TypedValue = sessionId });
            command.Parameters.AddWithValue("clearAssignment", clearsAssignment);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                task = MapTask(reader, GetTaskState(stateId));
            }
        }

        if (task is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        if (sessionId is not null)
        {
            const string sessionSql =
                """
                UPDATE coordination.agent_sessions
                SET current_task_id = CASE WHEN @clearAssignment THEN NULL ELSE @taskId END,
                    heartbeat_utc = NOW()
                WHERE id = @sessionId;
                """;

            await using var sessionCommand = new NpgsqlCommand(sessionSql, connection, transaction);
            sessionCommand.Parameters.AddWithValue("taskId", taskId);
            sessionCommand.Parameters.AddWithValue("sessionId", sessionId.Value);
            sessionCommand.Parameters.AddWithValue("clearAssignment", clearsAssignment);
            await sessionCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return task;
    }

    public async Task<TaskExecutionDto?> GetTaskExecutionAsync(Guid taskId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        const string taskExistsSql =
            """
            SELECT 1
            FROM coordination.tasks
            WHERE id = @taskId;
            """;

        await using (var existsCommand = new NpgsqlCommand(taskExistsSql, connection))
        {
            existsCommand.Parameters.AddWithValue("taskId", taskId);
            if (await existsCommand.ExecuteScalarAsync(cancellationToken) is null)
            {
                return null;
            }
        }

        var checklistItems = new List<TaskChecklistItemDto>();
        const string checklistSql =
            """
            SELECT id, task_id, ordinal, title, is_required, is_completed, completed_by_agent_session_id, completed_utc, created_utc, updated_utc
            FROM coordination.task_checklist_items
            WHERE task_id = @taskId
            ORDER BY ordinal ASC;
            """;

        await using (var checklistCommand = new NpgsqlCommand(checklistSql, connection))
        {
            checklistCommand.Parameters.AddWithValue("taskId", taskId);
            await using var reader = await checklistCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                checklistItems.Add(MapTaskChecklistItem(reader));
            }
        }

        var updates = new List<TaskUpdateDto>();
        const string updatesSql =
            """
            SELECT id, task_id, agent_session_id, update_kind, summary, details, created_utc
            FROM coordination.task_updates
            WHERE task_id = @taskId
            ORDER BY created_utc DESC, id DESC;
            """;

        await using (var updatesCommand = new NpgsqlCommand(updatesSql, connection))
        {
            updatesCommand.Parameters.AddWithValue("taskId", taskId);
            await using var reader = await updatesCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                updates.Add(MapTaskUpdate(reader));
            }
        }

        var artifacts = new List<TaskArtifactDto>();
        const string artifactsSql =
            """
            SELECT id, task_id, agent_session_id, artifact_kind, value, metadata, created_utc
            FROM coordination.task_artifacts
            WHERE task_id = @taskId
            ORDER BY created_utc DESC;
            """;

        await using (var artifactsCommand = new NpgsqlCommand(artifactsSql, connection))
        {
            artifactsCommand.Parameters.AddWithValue("taskId", taskId);
            await using var reader = await artifactsCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                artifacts.Add(MapTaskArtifact(reader));
            }
        }

        return new TaskExecutionDto(taskId, checklistItems, updates, artifacts);
    }

    public async Task<TaskChecklistItemDto?> AddTaskChecklistItemAsync(
        Guid taskId,
        string title,
        bool isRequired,
        int? ordinal,
        Guid? sessionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await TaskExistsAsync(connection, transaction, taskId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        if (sessionId is not null)
        {
            await TouchSessionAsync(connection, transaction, sessionId.Value, cancellationToken);
        }

        var nextOrdinal = ordinal ?? await GetNextChecklistOrdinalAsync(connection, transaction, taskId, cancellationToken);

        const string sql =
            """
            INSERT INTO coordination.task_checklist_items (
                id,
                task_id,
                ordinal,
                title,
                is_required)
            VALUES (
                @id,
                @taskId,
                @ordinal,
                @title,
                @isRequired)
            RETURNING id, task_id, ordinal, title, is_required, is_completed, completed_by_agent_session_id, completed_utc, created_utc, updated_utc;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("taskId", taskId);
        command.Parameters.AddWithValue("ordinal", nextOrdinal);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("isRequired", isRequired);

        TaskChecklistItemDto item;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            item = MapTaskChecklistItem(reader);
        }

        await transaction.CommitAsync(cancellationToken);
        return item;
    }

    public async Task<TaskChecklistItemDto?> CompleteTaskChecklistItemAsync(
        Guid taskId,
        Guid checklistItemId,
        bool isCompleted,
        Guid? sessionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (sessionId is not null)
        {
            await TouchSessionAsync(connection, transaction, sessionId.Value, cancellationToken);
        }

        const string sql =
            """
            UPDATE coordination.task_checklist_items
            SET is_completed = @isCompleted,
                completed_by_agent_session_id = CASE WHEN @isCompleted THEN @sessionId ELSE NULL END,
                completed_utc = CASE WHEN @isCompleted THEN NOW() ELSE NULL END,
                updated_utc = NOW()
            WHERE id = @checklistItemId
              AND task_id = @taskId
            RETURNING id, task_id, ordinal, title, is_required, is_completed, completed_by_agent_session_id, completed_utc, created_utc, updated_utc;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("taskId", taskId);
        command.Parameters.AddWithValue("checklistItemId", checklistItemId);
        command.Parameters.AddWithValue("isCompleted", isCompleted);
        command.Parameters.AddWithValue("sessionId", (object?)sessionId ?? DBNull.Value);

        TaskChecklistItemDto? item = null;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                item = MapTaskChecklistItem(reader);
            }
        }

        if (item is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await transaction.CommitAsync(cancellationToken);
        return item;
    }

    public async Task<TaskUpdateDto?> CreateTaskUpdateAsync(
        Guid taskId,
        Guid? sessionId,
        string updateKind,
        string summary,
        JsonDocument? details,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await TaskExistsAsync(connection, transaction, taskId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        if (sessionId is not null)
        {
            await TouchSessionAsync(connection, transaction, sessionId.Value, cancellationToken);
        }

        const string sql =
            """
            INSERT INTO coordination.task_updates (
                task_id,
                agent_session_id,
                update_kind,
                summary,
                details)
            VALUES (
                @taskId,
                @sessionId,
                @updateKind,
                @summary,
                @details)
            RETURNING id, task_id, agent_session_id, update_kind, summary, details, created_utc;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("taskId", taskId);
        command.Parameters.AddWithValue("sessionId", (object?)sessionId ?? DBNull.Value);
        command.Parameters.AddWithValue("updateKind", updateKind);
        command.Parameters.AddWithValue("summary", summary);
        command.Parameters.AddWithValue("details", details ?? EmptyJson);

        TaskUpdateDto update;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            update = MapTaskUpdate(reader);
        }

        await transaction.CommitAsync(cancellationToken);
        return update;
    }

    public async Task<TaskArtifactDto?> CreateTaskArtifactAsync(
        Guid taskId,
        Guid? sessionId,
        string artifactKind,
        string value,
        JsonDocument? metadata,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await TaskExistsAsync(connection, transaction, taskId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        if (sessionId is not null)
        {
            await TouchSessionAsync(connection, transaction, sessionId.Value, cancellationToken);
        }

        const string sql =
            """
            INSERT INTO coordination.task_artifacts (
                id,
                task_id,
                agent_session_id,
                artifact_kind,
                value,
                metadata)
            VALUES (
                @id,
                @taskId,
                @sessionId,
                @artifactKind,
                @value,
                @metadata)
            RETURNING id, task_id, agent_session_id, artifact_kind, value, metadata, created_utc;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("taskId", taskId);
        command.Parameters.AddWithValue("sessionId", (object?)sessionId ?? DBNull.Value);
        command.Parameters.AddWithValue("artifactKind", artifactKind);
        command.Parameters.AddWithValue("value", value);
        command.Parameters.AddWithValue("metadata", metadata ?? EmptyJson);

        TaskArtifactDto artifact;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            artifact = MapTaskArtifact(reader);
        }

        await transaction.CommitAsync(cancellationToken);
        return artifact;
    }

    public async Task<IReadOnlyList<AgentSessionDto>> ListAgentSessionsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT
                s.id,
                s.workspace_id,
                s.agent_id,
                s.agent_kind,
                s.state_id,
                s.display_name,
                s.current_task_id,
                s.capabilities,
                s.metadata,
                s.started_utc,
                s.heartbeat_utc,
                s.ended_utc,
                ss.key,
                ss.name
            FROM coordination.agent_sessions s
            JOIN coordination.agent_session_states ss ON ss.id = s.state_id
            WHERE s.workspace_id = @workspaceId
            ORDER BY s.heartbeat_utc DESC;
            """;

        var results = new List<AgentSessionDto>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspaceId", workspaceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(
                MapAgentSession(
                    reader,
                    new AgentSessionStateDto(
                        reader.GetInt32(4),
                        reader.GetString(12),
                        reader.GetString(13))));
        }

        return results;
    }

    public async Task<AgentSessionDto> RegisterAgentSessionAsync(
        Guid workspaceId,
        string agentId,
        string agentKind,
        string displayName,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO coordination.agent_sessions (
                id,
                workspace_id,
                agent_id,
                agent_kind,
                state_id,
                display_name,
                capabilities,
                metadata)
            VALUES (
                @id,
                @workspaceId,
                @agentId,
                @agentKind,
                1,
                @displayName,
                @capabilities,
                @metadata)
            RETURNING id, workspace_id, agent_id, agent_kind, state_id, display_name, current_task_id, capabilities, metadata, started_utc, heartbeat_utc, ended_utc;
            """;

        var id = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("workspaceId", workspaceId);
        command.Parameters.AddWithValue("agentId", agentId);
        command.Parameters.AddWithValue("agentKind", agentKind);
        command.Parameters.AddWithValue("displayName", displayName);
        command.Parameters.AddWithValue("capabilities", EmptyJson);
        command.Parameters.AddWithValue("metadata", EmptyJson);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return MapAgentSession(reader, GetAgentSessionState(1));
    }

    public async Task<AgentSessionDto?> RecordHeartbeatAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string updateSql =
            """
            UPDATE coordination.agent_sessions
            SET heartbeat_utc = NOW()
            WHERE id = @sessionId
            RETURNING id, workspace_id, agent_id, agent_kind, state_id, display_name, current_task_id, capabilities, metadata, started_utc, heartbeat_utc, ended_utc;
            """;

        await using var updateCommand = new NpgsqlCommand(updateSql, connection, transaction);
        updateCommand.Parameters.AddWithValue("sessionId", sessionId);

        AgentSessionDto? session = null;
        await using (var reader = await updateCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                session = MapAgentSession(reader, GetAgentSessionState(reader.GetInt32(4)));
            }
        }

        if (session is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        const string insertSql =
            """
            INSERT INTO coordination.agent_session_heartbeats (
                agent_session_id)
            VALUES (@sessionId);
            """;

        await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
        insertCommand.Parameters.AddWithValue("sessionId", sessionId);
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return session;
    }

    public async Task<AgentSessionDto?> CloseAgentSessionAsync(Guid sessionId, int stateId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        Guid? currentTaskId = null;
        const string currentTaskSql =
            """
            SELECT current_task_id
            FROM coordination.agent_sessions
            WHERE id = @sessionId
            FOR UPDATE;
            """;

        await using (var currentTaskCommand = new NpgsqlCommand(currentTaskSql, connection, transaction))
        {
            currentTaskCommand.Parameters.AddWithValue("sessionId", sessionId);
            await using var reader = await currentTaskCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken) && !reader.IsDBNull(0))
            {
                currentTaskId = reader.GetGuid(0);
            }
        }

        const string sessionSql =
            """
            UPDATE coordination.agent_sessions
            SET state_id = @stateId,
                ended_utc = NOW(),
                heartbeat_utc = NOW(),
                current_task_id = NULL
            WHERE id = @sessionId
            RETURNING id, workspace_id, agent_id, agent_kind, state_id, display_name, current_task_id, capabilities, metadata, started_utc, heartbeat_utc, ended_utc;
            """;

        AgentSessionDto? session = null;
        await using (var sessionCommand = new NpgsqlCommand(sessionSql, connection, transaction))
        {
            sessionCommand.Parameters.AddWithValue("sessionId", sessionId);
            sessionCommand.Parameters.AddWithValue("stateId", stateId);

            await using var reader = await sessionCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                currentTaskId = reader.IsDBNull(6) ? null : reader.GetGuid(6);
                session = MapAgentSession(reader, GetAgentSessionState(stateId));
            }
        }

        if (session is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        if (currentTaskId is not null)
        {
            const string taskSql =
                """
                UPDATE coordination.tasks
                SET assigned_agent_id = NULL,
                    state_id = 1,
                    updated_utc = NOW()
                WHERE id = @taskId
                  AND state_id = 2;
                """;

            await using var taskCommand = new NpgsqlCommand(taskSql, connection, transaction);
            taskCommand.Parameters.AddWithValue("taskId", currentTaskId.Value);
            await taskCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return session;
    }

    public async Task<IReadOnlyList<WorkspaceMessageDto>> ListWorkspaceMessagesAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT id, workspace_id, author_type, author_id, message_kind, content, metadata, created_utc
            FROM memory.workspace_messages
            WHERE workspace_id = @workspaceId
            ORDER BY created_utc DESC
            LIMIT 100;
            """;

        var results = new List<WorkspaceMessageDto>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspaceId", workspaceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapWorkspaceMessage(reader));
        }

        return results;
    }

    public async Task<WorkspaceMessageDto> CreateWorkspaceMessageAsync(
        Guid workspaceId,
        string authorType,
        string? authorId,
        string messageKind,
        string content,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO memory.workspace_messages (
                id,
                workspace_id,
                author_type,
                author_id,
                message_kind,
                content,
                metadata)
            VALUES (
                @id,
                @workspaceId,
                @authorType,
                @authorId,
                @messageKind,
                @content,
                @metadata)
            RETURNING id, workspace_id, author_type, author_id, message_kind, content, metadata, created_utc;
            """;

        var id = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("workspaceId", workspaceId);
        command.Parameters.AddWithValue("authorType", authorType);
        command.Parameters.AddWithValue("authorId", (object?)authorId ?? DBNull.Value);
        command.Parameters.AddWithValue("messageKind", messageKind);
        command.Parameters.AddWithValue("content", content);
        command.Parameters.AddWithValue("metadata", EmptyJson);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return MapWorkspaceMessage(reader);
    }

    private async Task EnsureDefaultBrainProfileAsync(CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO catalog.brain_profiles (
                id,
                name,
                description,
                metadata)
            VALUES (
                @id,
                @name,
                @description,
                @metadata)
            ON CONFLICT (id) DO NOTHING;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", DefaultBrainProfileId);
        command.Parameters.AddWithValue("name", "Infosphere");
        command.Parameters.AddWithValue("description", "Default collaborative coding brain profile.");
        command.Parameters.AddWithValue("metadata", EmptyJson);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static WorkspaceDto MapWorkspace(NpgsqlDataReader reader)
    {
        return new WorkspaceDto(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetFieldValue<JsonDocument>(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.GetFieldValue<DateTimeOffset>(7));
    }

    private static TaskDto MapTask(NpgsqlDataReader reader, TaskStateDto? state = null)
    {
        state ??= new TaskStateDto(reader.GetInt32(3), string.Empty, string.Empty);

        return new TaskDto(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            state,
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetInt32(5),
            reader.IsDBNull(6) ? null : reader.GetGuid(6),
            reader.GetFieldValue<DateTimeOffset>(7),
            reader.GetFieldValue<DateTimeOffset>(8));
    }

    private static TaskChecklistItemDto MapTaskChecklistItem(NpgsqlDataReader reader)
    {
        return new TaskChecklistItemDto(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetInt32(2),
            reader.GetString(3),
            reader.GetBoolean(4),
            reader.GetBoolean(5),
            reader.IsDBNull(6) ? null : reader.GetGuid(6),
            reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
            reader.GetFieldValue<DateTimeOffset>(8),
            reader.GetFieldValue<DateTimeOffset>(9));
    }

    private static TaskUpdateDto MapTaskUpdate(NpgsqlDataReader reader)
    {
        return new TaskUpdateDto(
            reader.GetInt64(0),
            reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetFieldValue<JsonDocument>(5),
            reader.GetFieldValue<DateTimeOffset>(6));
    }

    private static TaskArtifactDto MapTaskArtifact(NpgsqlDataReader reader)
    {
        return new TaskArtifactDto(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetFieldValue<JsonDocument>(5),
            reader.GetFieldValue<DateTimeOffset>(6));
    }

    private static AgentSessionDto MapAgentSession(NpgsqlDataReader reader, AgentSessionStateDto? state = null)
    {
        state ??= new AgentSessionStateDto(reader.GetInt32(4), string.Empty, string.Empty);

        return new AgentSessionDto(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            state,
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetGuid(6),
            reader.GetFieldValue<JsonDocument>(7),
            reader.GetFieldValue<JsonDocument>(8),
            reader.GetFieldValue<DateTimeOffset>(9),
            reader.GetFieldValue<DateTimeOffset>(10),
            reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11));
    }

    private static WorkspaceMessageDto MapWorkspaceMessage(NpgsqlDataReader reader)
    {
        return new WorkspaceMessageDto(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetFieldValue<JsonDocument>(6),
            reader.GetFieldValue<DateTimeOffset>(7));
    }

    private static TaskStateDto GetTaskState(int stateId)
    {
        return TaskStates.TryGetValue(stateId, out var state)
            ? state
            : new TaskStateDto(stateId, "unknown", "Unknown");
    }

    private static AgentSessionStateDto GetAgentSessionState(int stateId)
    {
        return AgentSessionStates.TryGetValue(stateId, out var state)
            ? state
            : new AgentSessionStateDto(stateId, "unknown", "Unknown");
    }

    private static async Task<bool> TaskExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT 1
            FROM coordination.tasks
            WHERE id = @taskId;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("taskId", taskId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<int> GetNextChecklistOrdinalAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT COALESCE(MAX(ordinal), 0) + 1
            FROM coordination.task_checklist_items
            WHERE task_id = @taskId;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("taskId", taskId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value);
    }

    private static async Task TouchSessionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            UPDATE coordination.agent_sessions
            SET heartbeat_utc = NOW()
            WHERE id = @sessionId;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("sessionId", sessionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
