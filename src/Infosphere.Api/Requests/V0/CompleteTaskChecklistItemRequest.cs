namespace Infosphere.Api.Requests.V0;

public sealed record CompleteTaskChecklistItemRequest(
    bool IsCompleted,
    Guid? SessionId);
