namespace Infosphere.Api.Dtos.V0;

public sealed record TaskExecutionDto(
    Guid TaskId,
    IReadOnlyList<TaskChecklistItemDto> ChecklistItems,
    IReadOnlyList<TaskUpdateDto> Updates,
    IReadOnlyList<TaskArtifactDto> Artifacts);
