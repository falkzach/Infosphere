using Microsoft.AspNetCore.Mvc;
using Infosphere.Api.Requests.V0;
using Infosphere.Api.Responses.V0;

namespace Infosphere.Api.Controllers.V0;

[ApiController]
[Route("api/v0/system")]
public sealed class SystemController : ControllerBase
{
    [HttpGet]
    public ActionResult<SystemOverviewResponse> Get([FromQuery] GetSystemRequest request)
    {
        return Ok(
            new SystemOverviewResponse(
                Service: "Infosphere.Api",
                ApiVersion: "v0",
                OpenApiDocumentPath: "/openapi/v0.json",
                DashboardPath: null,
                HealthPaths:
                [
                    "/healthz",
                    "/readyz",
                    "/startupz"
                ]));
    }
}
