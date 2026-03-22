using Infosphere.Api.Data;
using Infosphere.Api.Dtos.V0;
using Infosphere.Api.Requests.V0;
using Infosphere.Api.Responses.V0;
using Microsoft.AspNetCore.Mvc;

namespace Infosphere.Api.Controllers.V0;

[ApiController]
[Route("api/v0/tasks")]
public sealed class TasksController(InfosphereRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaskResponse>>> List(
        [FromQuery] ListTasksRequest request,
        CancellationToken cancellationToken)
    {
        var tasks = await repository.ListTasksAsync(request.WorkspaceId, request.AvailableOnly, cancellationToken);
        return Ok(tasks.Select(MapTask).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<TaskResponse>> Create(
        [FromBody] CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var task = await repository.CreateTaskAsync(
            request.WorkspaceId,
            request.Title,
            request.Priority,
            request.SuccessCriteria,
            cancellationToken);
        return CreatedAtAction(nameof(List), new { workspaceId = task.WorkspaceId }, MapTask(task));
    }

    [HttpPost("{taskId:guid}/claim")]
    public async Task<ActionResult<TaskResponse>> Claim(
        Guid taskId,
        [FromBody] ClaimTaskRequest request,
        CancellationToken cancellationToken)
    {
        var task = await repository.ClaimTaskAsync(taskId, request.SessionId, cancellationToken);
        return task is null ? Conflict() : Ok(MapTask(task));
    }

    [HttpPost("{taskId:guid}/state-transitions")]
    public async Task<ActionResult<TaskResponse>> Transition(
        Guid taskId,
        [FromBody] TransitionTaskRequest request,
        CancellationToken cancellationToken)
    {
        var task = await repository.TransitionTaskAsync(taskId, request.StateId, request.SessionId, cancellationToken);
        return task is null ? NotFound() : Ok(MapTask(task));
    }

    [HttpGet("{taskId:guid}/execution")]
    public async Task<ActionResult<TaskExecutionResponse>> GetExecution(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var execution = await repository.GetTaskExecutionAsync(taskId, cancellationToken);
        return execution is null ? NotFound() : Ok(MapExecution(execution));
    }

    [HttpPost("{taskId:guid}/checklist")]
    public async Task<ActionResult<TaskChecklistItemResponse>> AddChecklistItem(
        Guid taskId,
        [FromBody] AddTaskChecklistItemRequest request,
        CancellationToken cancellationToken)
    {
        var item = await repository.AddTaskChecklistItemAsync(
            taskId,
            request.Title,
            request.IsRequired,
            request.Ordinal,
            request.SessionId,
            cancellationToken);

        return item is null ? NotFound() : Ok(MapChecklistItem(item));
    }

    [HttpPost("{taskId:guid}/checklist/{checklistItemId:guid}/completion")]
    public async Task<ActionResult<TaskChecklistItemResponse>> CompleteChecklistItem(
        Guid taskId,
        Guid checklistItemId,
        [FromBody] CompleteTaskChecklistItemRequest request,
        CancellationToken cancellationToken)
    {
        var item = await repository.CompleteTaskChecklistItemAsync(
            taskId,
            checklistItemId,
            request.IsCompleted,
            request.SessionId,
            cancellationToken);

        return item is null ? NotFound() : Ok(MapChecklistItem(item));
    }

    [HttpPost("{taskId:guid}/updates")]
    public async Task<ActionResult<TaskUpdateResponse>> CreateUpdate(
        Guid taskId,
        [FromBody] CreateTaskUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var update = await repository.CreateTaskUpdateAsync(
            taskId,
            request.SessionId,
            request.UpdateKind,
            request.Summary,
            request.Details,
            cancellationToken);

        return update is null ? NotFound() : Ok(MapTaskUpdate(update));
    }

    [HttpPost("{taskId:guid}/artifacts")]
    public async Task<ActionResult<TaskArtifactResponse>> CreateArtifact(
        Guid taskId,
        [FromBody] CreateTaskArtifactRequest request,
        CancellationToken cancellationToken)
    {
        var artifact = await repository.CreateTaskArtifactAsync(
            taskId,
            request.SessionId,
            request.ArtifactKind,
            request.Value,
            request.Metadata,
            cancellationToken);

        return artifact is null ? NotFound() : Ok(MapTaskArtifact(artifact));
    }

    private static TaskResponse MapTask(TaskDto dto)
    {
        return new TaskResponse(
            dto.Id,
            dto.WorkspaceId,
            dto.Title,
            new TaskStateResponse(dto.State.Id, dto.State.Key, dto.State.Name),
            dto.AssignedAgentId,
            dto.Priority,
            dto.ContextEntryId,
            dto.CreatedUtc,
            dto.UpdatedUtc);
    }

    private static TaskExecutionResponse MapExecution(TaskExecutionDto dto)
    {
        return new TaskExecutionResponse(
            dto.TaskId,
            dto.ChecklistItems.Select(MapChecklistItem).ToArray(),
            dto.Updates.Select(MapTaskUpdate).ToArray(),
            dto.Artifacts.Select(MapTaskArtifact).ToArray());
    }

    private static TaskChecklistItemResponse MapChecklistItem(TaskChecklistItemDto dto)
    {
        return new TaskChecklistItemResponse(
            dto.Id,
            dto.TaskId,
            dto.Ordinal,
            dto.Title,
            dto.IsRequired,
            dto.IsCompleted,
            dto.CompletedByAgentSessionId,
            dto.CompletedUtc,
            dto.CreatedUtc,
            dto.UpdatedUtc);
    }

    private static TaskUpdateResponse MapTaskUpdate(TaskUpdateDto dto)
    {
        return new TaskUpdateResponse(
            dto.Id,
            dto.TaskId,
            dto.AgentSessionId,
            dto.UpdateKind,
            dto.Summary,
            dto.Details,
            dto.CreatedUtc);
    }

    private static TaskArtifactResponse MapTaskArtifact(TaskArtifactDto dto)
    {
        return new TaskArtifactResponse(
            dto.Id,
            dto.TaskId,
            dto.AgentSessionId,
            dto.ArtifactKind,
            dto.Value,
            dto.Metadata,
            dto.CreatedUtc);
    }
}
