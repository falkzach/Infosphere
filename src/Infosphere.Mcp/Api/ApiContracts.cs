using System.Text.Json;
using System.Net;

namespace Infosphere.Mcp.Api;

public sealed record WorkspaceSummary(
    Guid Id,
    Guid BrainProfileId,
    string Key,
    string Name,
    string Description,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record TaskSummary(
    Guid Id,
    Guid WorkspaceId,
    string Title,
    TaskStateSummary State,
    string? AssignedAgentId,
    int Priority,
    Guid? ContextEntryId,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record TaskExecutionSummary(
    Guid TaskId,
    IReadOnlyList<TaskChecklistItemSummary> ChecklistItems,
    IReadOnlyList<TaskUpdateSummary> Updates,
    IReadOnlyList<TaskArtifactSummary> Artifacts);

public sealed record TaskChecklistItemSummary(
    Guid Id,
    Guid TaskId,
    int Ordinal,
    string Title,
    bool IsRequired,
    bool IsCompleted,
    Guid? CompletedByAgentSessionId,
    DateTimeOffset? CompletedUtc,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record TaskUpdateSummary(
    long Id,
    Guid TaskId,
    Guid? AgentSessionId,
    string UpdateKind,
    string Summary,
    JsonDocument Details,
    DateTimeOffset CreatedUtc);

public sealed record TaskArtifactSummary(
    Guid Id,
    Guid TaskId,
    Guid? AgentSessionId,
    string ArtifactKind,
    string Value,
    JsonDocument Metadata,
    DateTimeOffset CreatedUtc);

public sealed record PaginatedTasksSummary(
    IReadOnlyList<TaskSummary> Items,
    int TotalCount,
    int Page,
    int Limit);

public sealed record TaskStateSummary(
    int Id,
    string Key,
    string Name);

public sealed record AgentSessionSummary(
    Guid Id,
    Guid WorkspaceId,
    string AgentId,
    string AgentKind,
    AgentSessionStateSummary State,
    string DisplayName,
    Guid? CurrentTaskId,
    DateTimeOffset StartedUtc,
    DateTimeOffset HeartbeatUtc,
    DateTimeOffset? EndedUtc);

public sealed record AgentSessionStateSummary(
    int Id,
    string Key,
    string Name);

public sealed record WorkspaceMessageSummary(
    Guid Id,
    Guid WorkspaceId,
    string AuthorType,
    string? AuthorId,
    string MessageKind,
    string Content,
    DateTimeOffset CreatedUtc);

public sealed record ClaimTaskRequest(
    Guid SessionId);

public sealed record TransitionTaskRequest(
    int StateId,
    Guid? SessionId);

public sealed record RegisterAgentSessionRequest(
    Guid WorkspaceId,
    string AgentId,
    string AgentKind,
    string DisplayName);

public sealed record CreateTaskRequest(
    Guid WorkspaceId,
    string Title,
    int Priority,
    IReadOnlyList<string>? SuccessCriteria);

public sealed record AddTaskChecklistItemRequest(
    string Title,
    bool IsRequired,
    int? Ordinal,
    Guid? SessionId);

public sealed record CompleteTaskChecklistItemRequest(
    bool IsCompleted,
    Guid? SessionId);

public sealed record CreateTaskUpdateRequest(
    Guid? SessionId,
    string UpdateKind,
    string Summary,
    JsonDocument? Details);

public sealed record CreateTaskArtifactRequest(
    Guid? SessionId,
    string ArtifactKind,
    string Value,
    JsonDocument? Metadata);

public sealed record CreateWorkspaceMessageRequest(
    Guid WorkspaceId,
    string AuthorType,
    string? AuthorId,
    string MessageKind,
    string Content);

public sealed record CloseAgentSessionRequest(
    int StateId = 5);

public sealed class McpApiException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
