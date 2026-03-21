using Infosphere.Api.Data;
using Infosphere.Api.Dtos.V0;
using Infosphere.Api.Requests.V0;
using Infosphere.Api.Responses.V0;
using Microsoft.AspNetCore.Mvc;

namespace Infosphere.Api.Controllers.V0;

[ApiController]
[Route("api/v0/workspace-messages")]
public sealed class WorkspaceMessagesController(InfosphereRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkspaceMessageResponse>>> List(
        [FromQuery] ListWorkspaceMessagesRequest request,
        CancellationToken cancellationToken)
    {
        var messages = await repository.ListWorkspaceMessagesAsync(request.WorkspaceId, cancellationToken);
        return Ok(messages.Select(MapMessage).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<WorkspaceMessageResponse>> Create(
        [FromBody] CreateWorkspaceMessageRequest request,
        CancellationToken cancellationToken)
    {
        var message = await repository.CreateWorkspaceMessageAsync(
            request.WorkspaceId,
            request.AuthorType,
            request.AuthorId,
            request.MessageKind,
            request.Content,
            cancellationToken);

        return CreatedAtAction(nameof(List), new { workspaceId = message.WorkspaceId }, MapMessage(message));
    }

    private static WorkspaceMessageResponse MapMessage(WorkspaceMessageDto dto)
    {
        return new WorkspaceMessageResponse(
            dto.Id,
            dto.WorkspaceId,
            dto.AuthorType,
            dto.AuthorId,
            dto.MessageKind,
            dto.Content,
            dto.CreatedUtc);
    }
}
