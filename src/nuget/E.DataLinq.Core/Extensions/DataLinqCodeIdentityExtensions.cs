using E.DataLinq.Core.Models.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;

namespace E.DataLinq.Core.Extensions;

static public class DataLinqCodeIdentityExtensions
{
    static public bool HasDataLinqCodeRole(this DataLinqCodeIdentity identity)
    {
        if (identity?.Roles == null)
        {
            return false;
        }

        return
           identity.Roles.Contains("datalinq-code") ||
           identity.Roles.Where(r => r.StartsWith("datalinq-code(") && r.EndsWith(")")).Count() > 0;
    }

    static public IEnumerable<string> DataLinqCodeRoleParameters(this DataLinqCodeIdentity identity)
    {
        return identity?.Roles.DataLinqCodeRoleParameters();
    }

    static public IEnumerable<string> DataLinqCodeRoleParameters(this IEnumerable<string> roles)
    {
        List<string> roleParameters = new List<string>();

        if (roles != null)
        {
            foreach (var role in roles.Where(r => r.StartsWith("datalinq-code(") && r.EndsWith(")")))
            {
                var parameters = role.Substring("datalinq-code(".Length, role.Length - "datalinq-code(".Length - 1)
                                     .Trim()
                                     .Split(',')
                                     .Select(p => p.Trim().ToLower())
                                     .Where(p => !String.IsNullOrEmpty(p));

                roleParameters.AddRange(parameters);
            }
        }

        return roleParameters.Distinct();
    }

    static public bool HasEndPointRoleParameter(this DataLinqCodeIdentity identity, string endPointId)
    {
        if (String.IsNullOrEmpty(endPointId))
        {
            return false;
        }

        var roleParameters = identity.DataLinqCodeRoleParameters();

        return roleParameters.Contains("*") || roleParameters.Contains(endPointId.ToLower());
    }

    static public bool HasRoleParameters(this DataLinqCodeIdentity identity, IEnumerable<string> roleParameters)
    {
        if (roleParameters == null || roleParameters.Count() == 0)
        {
            return true;
        }

        var userRoleParamters = identity.DataLinqCodeRoleParameters();

        var endPointRoleParameters = roleParameters.Where(p => !p.StartsWith("_"));
        if (endPointRoleParameters.Count() > 0 && !userRoleParamters.Contains("*"))
        {
            foreach (var endPointRoleParameter in endPointRoleParameters)
            {
                if (!userRoleParamters.Contains(endPointRoleParameter.ToLower()))
                {
                    return false;
                }
            }
        }

        var rightsRoleParameters = roleParameters.Where(p => p.StartsWith("_"));
        if (rightsRoleParameters.Count() > 0 && !userRoleParamters.Contains("_*"))
        {
            foreach (var rightsRoleParameter in rightsRoleParameters)
            {
                if (!userRoleParamters.Contains(rightsRoleParameter.ToLower()))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
