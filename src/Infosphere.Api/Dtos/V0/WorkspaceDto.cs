using System.Text.Json;

namespace Infosphere.Api.Dtos.V0;

public sealed record WorkspaceDto(
    Guid Id,
    Guid BrainProfileId,
    string Key,
    string Name,
    string Description,
    JsonDocument Metadata,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);
