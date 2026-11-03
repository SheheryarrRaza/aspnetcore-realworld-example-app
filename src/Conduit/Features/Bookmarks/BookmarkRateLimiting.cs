using System;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Conduit.Features.Bookmarks;

public static class BookmarkRateLimiting
{
    public const string PolicyName = "bookmarks";

    private const int PermitLimit = 120;

    /// <summary>
    /// Adds a per-user fixed-window limit for the bookmark endpoints; callers over it get 429.
    /// </summary>
    public static IServiceCollection AddBookmarkRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(
                PolicyName,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        httpContext.User.FindFirst("sub")?.Value
                            ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? httpContext.Connection.RemoteIpAddress?.ToString()
                            ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = PermitLimit,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                        }
                    )
            );
        });
}
