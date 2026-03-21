namespace Infosphere.Api.Responses.V0;

public sealed record WorkspaceSummaryResponse(
    Guid Id,
    Guid BrainProfileId,
    string Key,
    string Name,
    string Description,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);
