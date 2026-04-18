using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CMS.Infrastructure.Middleware;

// Runs after authentication so the JWT is already validated by the time I read it.
// I pull the 'tid' claim out of the token and store it in the scoped TenantContext
// so every downstream service knows which tenant this request belongs to.
//
// Pipeline order in Program.cs:
//   app.UseAuthentication();
//   app.UseAuthorization();
//   app.UseMiddleware<TenantResolverMiddleware>();  ← must be here
public sealed class TenantResolverMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolverMiddleware> _logger;

    // Keycloak sets this claim on every token via the realm's protocol mapper.
    private const string TenantClaimType = "tid";

    public TenantResolverMiddleware(RequestDelegate next, ILogger<TenantResolverMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        // Skip unauthenticated requests (login page, static files, etc.)
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var tidClaim = context.User.FindFirst(TenantClaimType);

        if (tidClaim is null || string.IsNullOrWhiteSpace(tidClaim.Value))
        {
            // If an authenticated user has no tid claim, something is wrong with the Keycloak config.
            _logger.LogWarning(
                "User {User} is authenticated but has no '{Claim}' claim. Returning 401.",
                context.User.Identity?.Name, TenantClaimType);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        tenantContext.TenantId = tidClaim.Value;

        _logger.LogDebug("Request scoped to tenant '{TenantId}' for path '{Path}'.",
            tenantContext.TenantId, context.Request.Path);

        await _next(context);
    }
}
