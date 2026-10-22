using E.DataLinq.Web.Middleware.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Mcp;

// Authenticates browser preview requests created by the get_preview_link MCP tool.
// Only registered in Test/Development. The token is bound to one view route; follow-up
// requests of the rendered report (select, css/js proxies) are limited to the same endpoint.
public class DataLinqMcpPreview
{
    public const string QueryParameter = "__mcp_preview";
    public const string CookieName = "datalinq-mcp-preview";

    private readonly RequestDelegate _next;

    public DataLinqMcpPreview(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, DataLinqMcpTokenStore store, IOptions<DataLinqMcpOptions> options)
    {
        if (context.User?.Identity?.IsAuthenticated != true &&
            TryGetDataLinqRoute(context.Request.Path, out var action, out var route))
        {
            string queryToken = context.Request.Query[QueryParameter];
            DataLinqMcpGrant grant = null;

            if (!String.IsNullOrEmpty(queryToken) && action.Equals("report", StringComparison.OrdinalIgnoreCase))
            {
                string sessionToken;
                (grant, sessionToken) = store.RedeemPreviewLink(queryToken, route, options.Value.PreviewSessionLifetimeSeconds);
                if (grant != null)
                {
                    context.Response.Cookies.Append(CookieName, sessionToken, new CookieOptions()
                    {
                        HttpOnly = true,
                        Secure = context.Request.IsHttps,
                        SameSite = SameSiteMode.Lax,
                        Path = context.Request.PathBase.Add("/datalinq").Value,
                        MaxAge = TimeSpan.FromSeconds(options.Value.PreviewSessionLifetimeSeconds)
                    });
                }
            }
            else if (context.Request.Cookies.TryGetValue(CookieName, out var cookieToken))
            {
                grant = store.GetPreviewGrant(cookieToken);
                if (grant != null && !IsSameEndpoint(grant.Route, route))
                {
                    grant = null;
                }
            }

            if (grant?.Identity != null)
            {
                var claims = new List<Claim>() { new Claim(ClaimTypes.Name, grant.Identity.Name) };
                if (!String.IsNullOrEmpty(grant.Identity.Id))
                {
                    claims.Add(new Claim(AuthConst.SubClaimType, grant.Identity.Id));
                }
                foreach (var role in grant.Identity.Roles ?? Array.Empty<string>())
                {
                    claims.Add(new Claim(AuthConst.RolesCaimType, role));
                }

                context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "DataLinqMcpPreview", ClaimTypes.Name, AuthConst.RolesCaimType));
            }
        }

        await _next(context);
    }

    // path: /datalinq/{action}/{__dataLinqRoute}
    static private bool TryGetDataLinqRoute(PathString path, out string action, out string route)
    {
        action = route = null;

        if (!path.StartsWithSegments("/datalinq", StringComparison.OrdinalIgnoreCase, out var remaining) ||
            !remaining.HasValue)
        {
            return false;
        }

        var parts = remaining.Value.Trim('/').Split('/');
        if (parts.Length != 2 || String.IsNullOrEmpty(parts[0]) || String.IsNullOrEmpty(parts[1]))
        {
            return false;
        }

        action = parts[0];
        route = Uri.UnescapeDataString(parts[1]);
        return true;
    }

    static private bool IsSameEndpoint(string grantRoute, string requestRoute)
    {
        var endpointId = grantRoute.Split('@')[0];
        var requestEndpointId = requestRoute.Split('@')[0];

        return endpointId.Equals(requestEndpointId, StringComparison.OrdinalIgnoreCase);
    }
}
