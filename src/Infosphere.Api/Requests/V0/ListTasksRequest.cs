namespace Infosphere.Api.Requests.V0;

public sealed record ListTasksRequest(
    Guid WorkspaceId,
    bool AvailableOnly = false,
    int Page = 1,
    int Limit = 25);
