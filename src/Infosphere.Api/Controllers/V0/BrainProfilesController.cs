using Infosphere.Api.Requests.V0;
using Infosphere.Api.Responses.V0;
using Infosphere.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace Infosphere.Api.Controllers.V0;

[ApiController]
[Route("api/v0/brain-profiles")]
public sealed class BrainProfilesController(InfosphereRepository repository) : ControllerBase
{
    [HttpGet("default")]
    public async Task<ActionResult<BrainProfileResponse>> GetDefault(
        [FromQuery] GetDefaultBrainProfileRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await repository.GetDefaultBrainProfileAsync(cancellationToken);
        var response = new BrainProfileResponse(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.CreatedUtc,
            entity.UpdatedUtc);

        return Ok(response);
    }
}
