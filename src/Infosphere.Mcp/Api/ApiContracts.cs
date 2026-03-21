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
    int Priority);

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
