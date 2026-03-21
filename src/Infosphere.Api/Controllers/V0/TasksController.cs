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
        var task = await repository.CreateTaskAsync(request.WorkspaceId, request.Title, request.Priority, cancellationToken);
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
}
