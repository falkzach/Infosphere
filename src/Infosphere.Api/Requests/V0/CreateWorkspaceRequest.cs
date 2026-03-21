namespace Infosphere.Api.Requests.V0;

public sealed record CreateWorkspaceRequest(
    string Key,
    string Name,
    string Description);
