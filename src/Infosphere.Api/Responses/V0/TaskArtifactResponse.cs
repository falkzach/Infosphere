using System.Text.Json;

namespace Infosphere.Api.Responses.V0;

public sealed record TaskArtifactResponse(
    Guid Id,
    Guid TaskId,
    Guid? AgentSessionId,
    string ArtifactKind,
    string Value,
    JsonDocument Metadata,
    DateTimeOffset CreatedUtc);
