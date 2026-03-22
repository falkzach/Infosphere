using System.Text.Json;

namespace Infosphere.Api.Dtos.V0;

public sealed record TaskArtifactDto(
    Guid Id,
    Guid TaskId,
    Guid? AgentSessionId,
    string ArtifactKind,
    string Value,
    JsonDocument Metadata,
    DateTimeOffset CreatedUtc);
