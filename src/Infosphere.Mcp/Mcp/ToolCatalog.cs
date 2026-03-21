using System.Text.Json;
using System.Text.Json.Nodes;
using Infosphere.Mcp.Api;

namespace Infosphere.Mcp.Mcp;

public sealed class ToolCatalog(IReadOnlyDictionary<string, ToolDefinition> tools)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public IReadOnlyList<object> ListTools()
    {
        return tools.Values
            .OrderBy(tool => tool.Name, StringComparer.Ordinal)
            .Select(
                tool => (object)new
                {
                    name = tool.Name,
                    description = tool.Description,
                    inputSchema = tool.InputSchema
                })
            .ToArray();
    }

    public async Task<ToolCallResult> CallAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!tools.TryGetValue(name, out var tool))
        {
            throw new McpMethodException(-32602, $"Unknown tool '{name}'.");
        }

        try
        {
            return await tool.Handler(arguments, cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new McpMethodException(-32602, $"Invalid arguments for tool '{name}': {exception.Message}");
        }
        catch (FormatException exception)
        {
            throw new McpMethodException(-32602, $"Invalid arguments for tool '{name}': {exception.Message}");
        }
    }

    public static ToolCatalog CreateDefault(InfosphereApiClient apiClient)
    {
        Dictionary<string, ToolDefinition> tools = new(StringComparer.Ordinal)
        {
            ["list_workspaces"] = new ToolDefinition(
                "list_workspaces",
                "List all workspaces currently known to Infosphere.",
                ToolSchema.Object(
                    properties: [],
                    required: []),
                async (_, cancellationToken) =>
                {
                    var workspaces = await apiClient.ListWorkspacesAsync(cancellationToken);
                    return ToolCallResult.FromValue(new { workspaces }, JsonOptions);
                }),
            ["register_agent_session"] = new ToolDefinition(
                "register_agent_session",
                "Register a new agent session in a workspace.",
                ToolSchema.Object(
                    properties:
                    [
                        ToolSchema.StringProperty("workspaceId", "Workspace identifier."),
                        ToolSchema.StringProperty("agentId", "Stable agent identifier."),
                        ToolSchema.StringProperty("agentKind", "Agent type or runtime name."),
                        ToolSchema.StringProperty("displayName", "Human-readable session label.")
                    ],
                    required: ["workspaceId", "agentId", "agentKind", "displayName"]),
                async (arguments, cancellationToken) =>
                {
                    var request = Deserialize<RegisterAgentSessionArguments>(arguments);
                    var session = await apiClient.RegisterAgentSessionAsync(
                        Guid.Parse(request.WorkspaceId),
                        request.AgentId,
                        request.AgentKind,
                        request.DisplayName,
                        cancellationToken);

                    return ToolCallResult.FromValue(new { session }, JsonOptions);
                }),
            ["close_agent_session"] = new ToolDefinition(
                "close_agent_session",
                "Close an existing agent session and release its current task if needed.",
                ToolSchema.Object(
                    properties:
                    [
                        ToolSchema.StringProperty("sessionId", "Agent session identifier."),
                        ToolSchema.IntegerProperty("stateId", "Closing state id, usually 5 for closed or 99 for error.", nullable: true)
                    ],
                    required: ["sessionId"]),
                async (arguments, cancellationToken) =>
                {
                    var request = Deserialize<CloseAgentSessionArguments>(arguments);
                    var session = await apiClient.CloseAgentSessionAsync(
                        Guid.Parse(request.SessionId),
                        request.StateId ?? 5,
                        cancellationToken);

                    return ToolCallResult.FromValue(new { session }, JsonOptions);
                }),
            ["heartbeat_agent_session"] = new ToolDefinition(
                "heartbeat_agent_session",
                "Record a heartbeat for an existing agent session.",
                ToolSchema.Object(
                    properties:
                    [
                        ToolSchema.StringProperty("sessionId", "Agent session identifier.")
                    ],
                    required: ["sessionId"]),
                async (arguments, cancellationToken) =>
                {
                    var request = Deserialize<HeartbeatAgentSessionArguments>(arguments);
                    var session = await apiClient.HeartbeatAgentSessionAsync(Guid.Parse(request.SessionId), cancellationToken);
                    return ToolCallResult.FromValue(new { session }, JsonOptions);
                }),
            ["create_task"] = new ToolDefinition(
                "create_task",
                "Create a new task in a workspace.",
                ToolSchema.Object(
                    properties:
                    [
                        ToolSchema.StringProperty("workspaceId", "Workspace identifier."),
                        ToolSchema.StringProperty("title", "Task title."),
                        ToolSchema.IntegerProperty("priority", "Task priority, higher means more urgent.")
                    ],
                    required: ["workspaceId", "title", "priority"]),
                async (arguments, cancellationToken) =>
                {
                    var request = Deserialize<CreateTaskArguments>(arguments);
                    var task = await apiClient.CreateTaskAsync(
                        Guid.Parse(request.WorkspaceId),
                        request.Title,
                        request.Priority,
                        cancellationToken);

                    return ToolCallResult.FromValue(new { task }, JsonOptions);
                }),
            ["list_tasks"] = new ToolDefinition(
                "list_tasks",
                "List tasks for a workspace.",
                ToolSchema.Object(
                    properties:
                    [
                        ToolSchema.StringProperty("workspaceId", "Workspace identifier.")
                    ],
                    required: ["workspaceId"]),
                async (arguments, cancellationToken) =>
                {
                    var request = Deserialize<ListTasksArguments>(arguments);
                    var tasks = await apiClient.ListTasksAsync(Guid.Parse(request.WorkspaceId), cancellationToken);
                    return ToolCallResult.FromValue(new { tasks }, JsonOptions);
                }),
            ["list_available_tasks"] = new ToolDefinition(
                "list_available_tasks",
                "List only ready and unclaimed tasks for a workspace.",
                ToolSchema.Object(
                    properties:
                    [
                        ToolSchema.StringProperty("workspaceId", "Workspace identifier.")
                    ],
                    required: ["workspaceId"]),
                async (arguments, cancellationToken) =>
                {
                    var request = Deserialize<ListTasksArguments>(arguments);
                    var tasks = await apiClient.ListAvailableTasksAsync(Guid.Parse(request.WorkspaceId), cancellationToken);
                    return ToolCallResult.FromValue(new { tasks }, JsonOptions);
                }),
            ["claim_task"] = new ToolDefinition(
                "claim_task",
                "Claim a task for a specific agent session.",
                ToolSchema.Object(
                    properties:
                    [
                        ToolSchema.StringProperty("taskId", "Task identifier."),
                        ToolSchema.StringProperty("sessionId", "Agent session identifier that is claiming the task.")
                    ],
                    required: ["taskId", "sessionId"]),
                async (arguments, cancellationToken) =>
                {
                    var request = Deserialize<ClaimTaskArguments>(arguments);
                    var task = await apiClient.ClaimTaskAsync(Guid.Parse(request.TaskId), Guid.Parse(request.SessionId), cancellationToken);
                    return ToolCallResult.FromValue(new { task }, JsonOptions);
                }),
            ["transition_task_state"] = new ToolDefinition(
                "transition_task_state",
                "Transition a task to a new integer state identifier.",
                ToolSchema.Object(
                    properties:
                    [
                        ToolSchema.StringProperty("taskId", "Task identifier."),
                        ToolSchema.IntegerProperty("stateId", "Target task state identifier."),
                        ToolSchema.StringProperty("sessionId", "Optional agent session identifier driving the transition.", nullable: true)
                    ],
                    required: ["taskId", "stateId"]),
                async (arguments, cancellationToken) =>
                {
                    var request = Deserialize<TransitionTaskArguments>(arguments);
                    var task = await apiClient.TransitionTaskAsync(
                        Guid.Parse(request.TaskId),
                        request.StateId,
                        request.SessionId is null ? null : Guid.Parse(request.SessionId),
                        cancellationToken);

                    return ToolCallResult.FromValue(new { task }, JsonOptions);
                }),
            ["list_workspace_messages"] = new ToolDefinition(
                "list_workspace_messages",
                "List workspace messages for a workspace.",
                ToolSchema.Object(
                    properties:
                    [
                        ToolSchema.StringProperty("workspaceId", "Workspace identifier.")
                    ],
                    required: ["workspaceId"]),
                async (arguments, cancellationToken) =>
                {
                    var request = Deserialize<ListWorkspaceMessagesArguments>(arguments);
                    var messages = await apiClient.ListWorkspaceMessagesAsync(Guid.Parse(request.WorkspaceId), cancellationToken);
                    return ToolCallResult.FromValue(new { messages }, JsonOptions);
                }),
            ["post_workspace_message"] = new ToolDefinition(
                "post_workspace_message",
                "Post a workspace message from a human or agent author.",
                ToolSchema.Object(
                    properties:
                    [
                        ToolSchema.StringProperty("workspaceId", "Workspace identifier."),
                        ToolSchema.StringProperty("authorType", "Author type such as 'human' or 'agent'."),
                        ToolSchema.StringProperty("authorId", "Optional author identifier.", nullable: true),
                        ToolSchema.StringProperty("messageKind", "Message classification."),
                        ToolSchema.StringProperty("content", "Message body.")
                    ],
                    required: ["workspaceId", "authorType", "messageKind", "content"]),
                async (arguments, cancellationToken) =>
                {
                    var request = Deserialize<PostWorkspaceMessageArguments>(arguments);
                    var message = await apiClient.PostWorkspaceMessageAsync(
                        Guid.Parse(request.WorkspaceId),
                        request.AuthorType,
                        request.AuthorId,
                        request.MessageKind,
                        request.Content,
                        cancellationToken);

                    return ToolCallResult.FromValue(new { message }, JsonOptions);
                })
        };

        return new ToolCatalog(tools);
    }

    private static T Deserialize<T>(JsonElement arguments)
    {
        var value = arguments.Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });

        return value ?? throw new JsonException($"Could not deserialize arguments into {typeof(T).Name}.");
    }

    public sealed record RegisterAgentSessionArguments(
        string WorkspaceId,
        string AgentId,
        string AgentKind,
        string DisplayName);

    public sealed record HeartbeatAgentSessionArguments(
        string SessionId);

    public sealed record CloseAgentSessionArguments(
        string SessionId,
        int? StateId);

    public sealed record CreateTaskArguments(
        string WorkspaceId,
        string Title,
        int Priority);

    public sealed record ListTasksArguments(
        string WorkspaceId);

    public sealed record ClaimTaskArguments(
        string TaskId,
        string SessionId);

    public sealed record TransitionTaskArguments(
        string TaskId,
        int StateId,
        string? SessionId);

    public sealed record ListWorkspaceMessagesArguments(
        string WorkspaceId);

    public sealed record PostWorkspaceMessageArguments(
        string WorkspaceId,
        string AuthorType,
        string? AuthorId,
        string MessageKind,
        string Content);
}

public sealed record ToolDefinition(
    string Name,
    string Description,
    JsonObject InputSchema,
    Func<JsonElement, CancellationToken, Task<ToolCallResult>> Handler);

public sealed record ToolCallResult(
    string Text,
    object StructuredContent,
    bool IsError)
{
    public static ToolCallResult FromValue(object value, JsonSerializerOptions jsonOptions)
    {
        return new ToolCallResult(
            JsonSerializer.Serialize(value, jsonOptions),
            value,
            false);
    }
}

public static class ToolSchema
{
    public static JsonObject Object(IReadOnlyList<KeyValuePair<string, JsonNode?>> properties, IReadOnlyList<string> required)
    {
        var propertyObject = new JsonObject();
        foreach (var property in properties)
        {
            propertyObject[property.Key] = property.Value;
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = propertyObject,
            ["required"] = new JsonArray(required.Select(value => (JsonNode?)value).ToArray()),
            ["additionalProperties"] = false
        };
    }

    public static KeyValuePair<string, JsonNode?> StringProperty(string name, string description, bool nullable = false)
    {
        return new KeyValuePair<string, JsonNode?>(
            name,
            new JsonObject
            {
                ["type"] = nullable ? new JsonArray("string", "null") : "string",
                ["description"] = description
            });
    }

    public static KeyValuePair<string, JsonNode?> IntegerProperty(string name, string description, bool nullable = false)
    {
        return new KeyValuePair<string, JsonNode?>(
            name,
            new JsonObject
            {
                ["type"] = nullable ? new JsonArray("integer", "null") : "integer",
                ["description"] = description
            });
    }
}
