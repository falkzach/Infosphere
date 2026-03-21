namespace Infosphere.Api.Requests.V0;

public sealed record CreateWorkspaceMessageRequest(
    Guid WorkspaceId,
    string AuthorType,
    string? AuthorId,
    string MessageKind,
    string Content);
