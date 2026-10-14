using Microsoft.AspNetCore.Mvc;

namespace Conduit.Features.Status;

[Route("status")]
public class StatusController : Controller
{
    /// <summary>
    /// Reports that the API is running; available without authentication.
    /// </summary>
    [HttpGet]
    public StatusEnvelope Get() => new("ok");
}
