namespace Infosphere.Api.Responses.V0;

public sealed record TaskExecutionResponse(
    Guid TaskId,
    IReadOnlyList<TaskChecklistItemResponse> ChecklistItems,
    IReadOnlyList<TaskUpdateResponse> Updates,
    IReadOnlyList<TaskArtifactResponse> Artifacts);
