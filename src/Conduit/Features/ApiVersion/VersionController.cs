using Microsoft.AspNetCore.Mvc;

namespace Conduit.Features.ApiVersion;

[Route("version")]
public class VersionController : Controller
{
    /// <summary>Returns the API assembly version in major.minor.build form.</summary>
    [HttpGet]
    public VersionEnvelope Get() => new(typeof(Program).Assembly.GetName().Version!.ToString(3));
}
