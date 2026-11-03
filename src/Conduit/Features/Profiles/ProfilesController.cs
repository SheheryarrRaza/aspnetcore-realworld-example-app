using System.Threading;
using System.Threading.Tasks;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Conduit.Features.Profiles;

[Route("profiles")]
public class ProfilesController(IMediator mediator) : Controller
{
    [HttpGet("{username}")]
    public ValueTask<ProfileEnvelope> Get(string username, CancellationToken cancellationToken) =>
        mediator.Send(new Details.Query(username), cancellationToken);

    /// <summary>
    /// Returns the favourite count of the named profile, rate limited per client.
    /// </summary>
    [EnableRateLimiting("profile-counts")]
    [HttpGet("{username}/counts")]
    public ValueTask<ProfileCountsEnvelope> GetCounts(
        string username,
        CancellationToken cancellationToken
    ) => mediator.Send(new Counts.Query(username), cancellationToken);
}
