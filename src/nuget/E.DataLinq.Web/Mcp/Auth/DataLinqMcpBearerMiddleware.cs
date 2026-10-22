using E.DataLinq.Web.Middleware.Authentication;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Mcp;

// Requires a valid MCP bearer token for the MCP transport endpoint.
// Existing DataLinq cookies/datalinq-token headers are intentionally ignored here.
public class DataLinqMcpBearerMiddleware
{
    public const string AuthenticationType = "DataLinqMcp";

    private readonly RequestDelegate _next;

    public DataLinqMcpBearerMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, DataLinqMcpTokenStore store)
    {
        if (!IsMcpTransportRequest(context.Request.Path))
        {
            await _next(context);
            return;
        }

        string authorization = context.Request.Headers.Authorization.ToString();
        string token = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization.Substring("Bearer ".Length).Trim()
            : null;

        var grant = store.ValidateAgentToken(token);
        if (grant?.Identity == null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer realm=\"datalinq-mcp\"";
            return;
        }

        var claims = new List<Claim>()
        {
            new Claim(ClaimTypes.Name, grant.Identity.Name)
        };
        if (!String.IsNullOrEmpty(grant.Identity.Id))
        {
            claims.Add(new Claim(AuthConst.SubClaimType, grant.Identity.Id));
        }
        foreach (var role in grant.Identity.Roles ?? Array.Empty<string>())
        {
            claims.Add(new Claim(AuthConst.RolesCaimType, role));
        }

        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationType, ClaimTypes.Name, AuthConst.RolesCaimType));

        await _next(context);
    }

    static private bool IsMcpTransportRequest(PathString path)
        => path.StartsWithSegments(DataLinqMcpOptions.RoutePrefix, StringComparison.OrdinalIgnoreCase);
}
