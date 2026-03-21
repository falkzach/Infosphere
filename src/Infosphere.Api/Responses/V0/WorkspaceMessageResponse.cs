namespace Infosphere.Api.Responses.V0;

public sealed record WorkspaceMessageResponse(
    Guid Id,
    Guid WorkspaceId,
    string AuthorType,
    string? AuthorId,
    string MessageKind,
    string Content,
    DateTimeOffset CreatedUtc);
