using System.Text.Json;

namespace Infosphere.Api.Dtos.V0;

public sealed record BrainProfileDto(
    Guid Id,
    string Name,
    string Description,
    JsonDocument Metadata,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);
