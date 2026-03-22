using System.Text.Json;

namespace Infosphere.Api.Dtos.V0;

public sealed record TaskUpdateDto(
    long Id,
    Guid TaskId,
    Guid? AgentSessionId,
    string UpdateKind,
    string Summary,
    JsonDocument Details,
    DateTimeOffset CreatedUtc);
