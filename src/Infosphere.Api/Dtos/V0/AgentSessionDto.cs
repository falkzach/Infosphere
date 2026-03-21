using System.Text.Json;

namespace Infosphere.Api.Dtos.V0;

public sealed record AgentSessionDto(
    Guid Id,
    Guid WorkspaceId,
    string AgentId,
    string AgentKind,
    AgentSessionStateDto State,
    string DisplayName,
    Guid? CurrentTaskId,
    JsonDocument Capabilities,
    JsonDocument Metadata,
    DateTimeOffset StartedUtc,
    DateTimeOffset HeartbeatUtc,
    DateTimeOffset? EndedUtc);
