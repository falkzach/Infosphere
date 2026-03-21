namespace Infosphere.Api.Requests.V0;

public sealed record RegisterAgentSessionRequest(
    Guid WorkspaceId,
    string AgentId,
    string AgentKind,
    string DisplayName);
