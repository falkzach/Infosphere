using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Infosphere.Mcp.Api;

public sealed class InfosphereApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<IReadOnlyList<WorkspaceSummary>> ListWorkspacesAsync(CancellationToken cancellationToken)
    {
        return GetRequiredAsync<IReadOnlyList<WorkspaceSummary>>("api/v0/workspaces", cancellationToken);
    }

    public Task<IReadOnlyList<TaskSummary>> ListTasksAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        return GetRequiredAsync<IReadOnlyList<TaskSummary>>($"api/v0/tasks?workspaceId={workspaceId:D}", cancellationToken);
    }

    public Task<IReadOnlyList<TaskSummary>> ListAvailableTasksAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        return GetRequiredAsync<IReadOnlyList<TaskSummary>>(
            $"api/v0/tasks?workspaceId={workspaceId:D}&availableOnly=true",
            cancellationToken);
    }

    public Task<TaskSummary> CreateTaskAsync(Guid workspaceId, string title, int priority, CancellationToken cancellationToken)
    {
        return SendRequiredAsync<TaskSummary>(
            HttpMethod.Post,
            "api/v0/tasks",
            new CreateTaskRequest(workspaceId, title, priority, null),
            cancellationToken);
    }

    public Task<TaskSummary> CreateTaskAsync(
        Guid workspaceId,
        string title,
        int priority,
        IReadOnlyList<string>? successCriteria,
        CancellationToken cancellationToken)
    {
        return SendRequiredAsync<TaskSummary>(
            HttpMethod.Post,
            "api/v0/tasks",
            new CreateTaskRequest(workspaceId, title, priority, successCriteria),
            cancellationToken);
    }

    public Task<TaskSummary> ClaimTaskAsync(Guid taskId, Guid sessionId, CancellationToken cancellationToken)
    {
        return SendRequiredAsync<TaskSummary>(
            HttpMethod.Post,
            $"api/v0/tasks/{taskId:D}/claim",
            new ClaimTaskRequest(sessionId),
            cancellationToken);
    }

    public Task<TaskSummary> TransitionTaskAsync(
        Guid taskId,
        int stateId,
        Guid? sessionId,
        CancellationToken cancellationToken)
    {
        return SendRequiredAsync<TaskSummary>(
            HttpMethod.Post,
            $"api/v0/tasks/{taskId:D}/state-transitions",
            new TransitionTaskRequest(stateId, sessionId),
            cancellationToken);
    }

    public Task<TaskExecutionSummary> GetTaskExecutionAsync(Guid taskId, CancellationToken cancellationToken)
    {
        return GetRequiredAsync<TaskExecutionSummary>($"api/v0/tasks/{taskId:D}/execution", cancellationToken);
    }

    public Task<TaskChecklistItemSummary> AddTaskChecklistItemAsync(
        Guid taskId,
        string title,
        bool isRequired,
        int? ordinal,
        Guid? sessionId,
        CancellationToken cancellationToken)
    {
        return SendRequiredAsync<TaskChecklistItemSummary>(
            HttpMethod.Post,
            $"api/v0/tasks/{taskId:D}/checklist",
            new AddTaskChecklistItemRequest(title, isRequired, ordinal, sessionId),
            cancellationToken);
    }

    public Task<TaskChecklistItemSummary> CompleteTaskChecklistItemAsync(
        Guid taskId,
        Guid checklistItemId,
        bool isCompleted,
        Guid? sessionId,
        CancellationToken cancellationToken)
    {
        return SendRequiredAsync<TaskChecklistItemSummary>(
            HttpMethod.Post,
            $"api/v0/tasks/{taskId:D}/checklist/{checklistItemId:D}/completion",
            new CompleteTaskChecklistItemRequest(isCompleted, sessionId),
            cancellationToken);
    }

    public Task<TaskUpdateSummary> CreateTaskUpdateAsync(
        Guid taskId,
        Guid? sessionId,
        string updateKind,
        string summary,
        JsonDocument? details,
        CancellationToken cancellationToken)
    {
        return SendRequiredAsync<TaskUpdateSummary>(
            HttpMethod.Post,
            $"api/v0/tasks/{taskId:D}/updates",
            new CreateTaskUpdateRequest(sessionId, updateKind, summary, details),
            cancellationToken);
    }

    public Task<TaskArtifactSummary> CreateTaskArtifactAsync(
        Guid taskId,
        Guid? sessionId,
        string artifactKind,
        string value,
        JsonDocument? metadata,
        CancellationToken cancellationToken)
    {
        return SendRequiredAsync<TaskArtifactSummary>(
            HttpMethod.Post,
            $"api/v0/tasks/{taskId:D}/artifacts",
            new CreateTaskArtifactRequest(sessionId, artifactKind, value, metadata),
            cancellationToken);
    }

    public Task<AgentSessionSummary> RegisterAgentSessionAsync(
        Guid workspaceId,
        string agentId,
        string agentKind,
        string displayName,
        CancellationToken cancellationToken)
    {
        return SendRequiredAsync<AgentSessionSummary>(
            HttpMethod.Post,
            "api/v0/agent-sessions",
            new RegisterAgentSessionRequest(workspaceId, agentId, agentKind, displayName),
            cancellationToken);
    }

    public Task<AgentSessionSummary> HeartbeatAgentSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return SendRequiredAsync<AgentSessionSummary>(
            HttpMethod.Post,
            $"api/v0/agent-sessions/{sessionId:D}/heartbeats",
            body: null,
            cancellationToken);
    }

    public Task<AgentSessionSummary> CloseAgentSessionAsync(Guid sessionId, int stateId, CancellationToken cancellationToken)
    {
        return SendRequiredAsync<AgentSessionSummary>(
            HttpMethod.Post,
            $"api/v0/agent-sessions/{sessionId:D}/close",
            new CloseAgentSessionRequest(stateId),
            cancellationToken);
    }

    public Task<IReadOnlyList<WorkspaceMessageSummary>> ListWorkspaceMessagesAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        return GetRequiredAsync<IReadOnlyList<WorkspaceMessageSummary>>(
            $"api/v0/workspace-messages?workspaceId={workspaceId:D}",
            cancellationToken);
    }

    public Task<WorkspaceMessageSummary> PostWorkspaceMessageAsync(
        Guid workspaceId,
        string authorType,
        string? authorId,
        string messageKind,
        string content,
        CancellationToken cancellationToken)
    {
        return SendRequiredAsync<WorkspaceMessageSummary>(
            HttpMethod.Post,
            "api/v0/workspace-messages",
            new CreateWorkspaceMessageRequest(workspaceId, authorType, authorId, messageKind, content),
            cancellationToken);
    }

    private async Task<T> GetRequiredAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(path, cancellationToken);
        return await ReadRequiredResponseAsync<T>(response, cancellationToken);
    }

    private async Task<T> SendRequiredAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadRequiredResponseAsync<T>(response, cancellationToken);
    }

    private static async Task<T> ReadRequiredResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            if (payload is null)
            {
                throw new McpApiException("The Infosphere API returned an empty response body.", HttpStatusCode.InternalServerError);
            }

            return payload;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = string.IsNullOrWhiteSpace(body)
            ? $"Infosphere API request failed with status code {(int)response.StatusCode}."
            : $"Infosphere API request failed with status code {(int)response.StatusCode}: {body}";

        throw new McpApiException(message, response.StatusCode);
    }
}
