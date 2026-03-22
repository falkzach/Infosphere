namespace Infosphere.Api.Dtos.V0;

public sealed record TaskChecklistItemDto(
    Guid Id,
    Guid TaskId,
    int Ordinal,
    string Title,
    bool IsRequired,
    bool IsCompleted,
    Guid? CompletedByAgentSessionId,
    DateTimeOffset? CompletedUtc,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);
