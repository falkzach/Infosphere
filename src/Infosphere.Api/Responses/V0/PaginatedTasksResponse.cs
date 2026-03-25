namespace Infosphere.Api.Responses.V0;

public sealed record PaginatedTasksResponse(
    IReadOnlyList<TaskResponse> Items,
    int TotalCount,
    int Page,
    int Limit);
