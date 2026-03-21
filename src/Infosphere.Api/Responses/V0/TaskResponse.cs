namespace Infosphere.Api.Responses.V0;

public sealed record TaskResponse(
    Guid Id,
    Guid WorkspaceId,
    string Title,
    TaskStateResponse State,
    string? AssignedAgentId,
    int Priority,
    Guid? ContextEntryId,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);
