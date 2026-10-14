using Microsoft.AspNetCore.Mvc;

namespace Conduit.Features.Ping;

[Route("ping")]
public class PingController : Controller
{
    /// <summary>Returns a constant pong response so clients can check the API is reachable.</summary>
    [HttpGet]
    public PingEnvelope Get() => new(true);
}
