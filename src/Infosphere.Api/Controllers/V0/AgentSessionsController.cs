using Infosphere.Api.Data;
using Infosphere.Api.Dtos.V0;
using Infosphere.Api.Requests.V0;
using Infosphere.Api.Responses.V0;
using Microsoft.AspNetCore.Mvc;

namespace Infosphere.Api.Controllers.V0;

[ApiController]
[Route("api/v0/agent-sessions")]
public sealed class AgentSessionsController(InfosphereRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AgentSessionResponse>>> List(
        [FromQuery] ListAgentSessionsRequest request,
        CancellationToken cancellationToken)
    {
        var sessions = await repository.ListAgentSessionsAsync(request.WorkspaceId, cancellationToken);
        return Ok(sessions.Select(MapSession).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<AgentSessionResponse>> Register(
        [FromBody] RegisterAgentSessionRequest request,
        CancellationToken cancellationToken)
    {
        var session = await repository.RegisterAgentSessionAsync(
            request.WorkspaceId,
            request.AgentId,
            request.AgentKind,
            request.DisplayName,
            cancellationToken);

        return CreatedAtAction(nameof(List), new { workspaceId = session.WorkspaceId }, MapSession(session));
    }

    [HttpPost("{sessionId:guid}/heartbeats")]
    public async Task<ActionResult<AgentSessionResponse>> Heartbeat(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await repository.RecordHeartbeatAsync(sessionId, cancellationToken);
        return session is null ? NotFound() : Ok(MapSession(session));
    }

    [HttpPost("{sessionId:guid}/close")]
    public async Task<ActionResult<AgentSessionResponse>> Close(
        Guid sessionId,
        [FromBody] CloseAgentSessionRequest request,
        CancellationToken cancellationToken)
    {
        var session = await repository.CloseAgentSessionAsync(sessionId, request.StateId, cancellationToken);
        return session is null ? NotFound() : Ok(MapSession(session));
    }

    private static AgentSessionResponse MapSession(AgentSessionDto dto)
    {
        return new AgentSessionResponse(
            dto.Id,
            dto.WorkspaceId,
            dto.AgentId,
            dto.AgentKind,
            new AgentSessionStateResponse(dto.State.Id, dto.State.Key, dto.State.Name),
            dto.DisplayName,
            dto.CurrentTaskId,
            dto.StartedUtc,
            dto.HeartbeatUtc,
            dto.EndedUtc);
    }
}
