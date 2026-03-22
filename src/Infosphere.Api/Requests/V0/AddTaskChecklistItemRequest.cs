namespace Infosphere.Api.Requests.V0;

public sealed record AddTaskChecklistItemRequest(
    string Title,
    bool IsRequired,
    int? Ordinal,
    Guid? SessionId);
