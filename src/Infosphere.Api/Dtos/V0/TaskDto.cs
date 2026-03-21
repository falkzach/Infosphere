namespace Infosphere.Api.Dtos.V0;

public sealed record TaskDto(
    Guid Id,
    Guid WorkspaceId,
    string Title,
    TaskStateDto State,
    string? AssignedAgentId,
    int Priority,
    Guid? ContextEntryId,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);
