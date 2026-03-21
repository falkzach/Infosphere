namespace Infosphere.Api.Responses.V0;

public sealed record AgentSessionResponse(
    Guid Id,
    Guid WorkspaceId,
    string AgentId,
    string AgentKind,
    AgentSessionStateResponse State,
    string DisplayName,
    Guid? CurrentTaskId,
    DateTimeOffset StartedUtc,
    DateTimeOffset HeartbeatUtc,
    DateTimeOffset? EndedUtc);
