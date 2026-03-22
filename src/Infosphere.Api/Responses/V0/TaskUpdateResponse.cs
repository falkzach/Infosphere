using System.Text.Json;

namespace Infosphere.Api.Responses.V0;

public sealed record TaskUpdateResponse(
    long Id,
    Guid TaskId,
    Guid? AgentSessionId,
    string UpdateKind,
    string Summary,
    JsonDocument Details,
    DateTimeOffset CreatedUtc);
