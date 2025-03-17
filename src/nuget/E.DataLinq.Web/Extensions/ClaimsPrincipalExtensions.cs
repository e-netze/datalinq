using E.DataLinq.Web.Middleware.Authentication;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace E.DataLinq.Web.Extensions;

static class ClaimsPrincipalExtensions
{
    static public IEnumerable<string> GetRoles(this ClaimsPrincipal claimsPrincipal)
    {
        if (claimsPrincipal?.Claims == null)
        {
            return new string[0];
        }

        var roles = claimsPrincipal
                .Claims
                .Where(c => c.Type == AuthConst.RolesCaimType)
                .Select(c => c.Value)
                .ToArray();

        if (roles == null || roles.Length == 0)
        {
            var roleClaim = claimsPrincipal
                  .Claims
                  .Where(c => c.Type == "role")
                  .FirstOrDefault();

            if (roleClaim != null && roleClaim.Value != null && roleClaim.Value.StartsWith("["))
            {
                try
                {
                    return JsonConvert.DeserializeObject<string[]>(roleClaim.Value);
                }
                catch { }
            }
        }

        return roles;
    }

    static public bool HasRole(this ClaimsPrincipal claimsPrincipal, string role)
    {
        return claimsPrincipal.GetRoles().Contains(role);
    }

    static public string GetUsername(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal.Identity.Name;
    }

    static public string TryGetUsername(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal?.Identity?.Name;
    }

    static public string GetUserId(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal?
                    .Claims?
                    .Where(c => c.Type == AuthConst.SubClaimType || c.Type == "sub")
                    .FirstOrDefault()?
                    .Value;
    }
}
