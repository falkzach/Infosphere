namespace Infosphere.Api.Requests.V0;

public sealed record CreateTaskRequest(
    Guid WorkspaceId,
    string Title,
    int Priority);
