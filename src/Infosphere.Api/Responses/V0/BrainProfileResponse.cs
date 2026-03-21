namespace Infosphere.Api.Responses.V0;

public sealed record BrainProfileResponse(
    Guid Id,
    string Name,
    string Description,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);
