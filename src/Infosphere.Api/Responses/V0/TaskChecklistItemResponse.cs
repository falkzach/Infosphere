namespace Infosphere.Api.Responses.V0;

public sealed record TaskChecklistItemResponse(
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
