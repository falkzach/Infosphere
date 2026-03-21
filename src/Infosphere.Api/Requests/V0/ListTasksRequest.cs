namespace Infosphere.Api.Requests.V0;

public sealed record ListTasksRequest(
    Guid WorkspaceId,
    bool AvailableOnly = false);
