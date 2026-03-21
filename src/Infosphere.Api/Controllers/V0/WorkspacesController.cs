using Infosphere.Api.Data;
using Infosphere.Api.Dtos.V0;
using Microsoft.AspNetCore.Mvc;
using Infosphere.Api.Requests.V0;
using Infosphere.Api.Responses.V0;

namespace Infosphere.Api.Controllers.V0;

[ApiController]
[Route("api/v0/workspaces")]
public sealed class WorkspacesController(InfosphereRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkspaceSummaryResponse>>> List(
        [FromQuery] ListWorkspacesRequest request,
        CancellationToken cancellationToken)
    {
        var workspaces = await repository.ListWorkspacesAsync(cancellationToken);
        return Ok(workspaces.Select(MapWorkspace).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<WorkspaceSummaryResponse>> Create(
        [FromBody] CreateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        var workspace = await repository.CreateWorkspaceAsync(
            request.Key,
            request.Name,
            request.Description,
            cancellationToken);

        return CreatedAtAction(nameof(List), MapWorkspace(workspace));
    }

    private static WorkspaceSummaryResponse MapWorkspace(WorkspaceDto dto)
    {
        return new WorkspaceSummaryResponse(
            dto.Id,
            dto.BrainProfileId,
            dto.Key,
            dto.Name,
            dto.Description,
            dto.CreatedUtc,
            dto.UpdatedUtc);
    }
}
