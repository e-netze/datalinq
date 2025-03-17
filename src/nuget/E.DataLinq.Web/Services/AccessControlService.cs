using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace E.DataLinq.Web.Services;

public class AccessControlService
{
    private readonly DataLinqOptions _options;

    public AccessControlService(IOptionsMonitor<DataLinqOptions> optionsMonitor)
    {
        _options = optionsMonitor.CurrentValue;
    }

    public string AppendUserPrefix(string username, string prefix)
    {
        if (String.IsNullOrEmpty(prefix) ||
            String.IsNullOrWhiteSpace(username) ||
            username == "*" ||
            username.StartsWith($"{prefix}::"))
        {
            return username;
        }

        return $"{prefix}::{username}";
    }

    public string[] AppendUserPrefix(string[] names, string prefix)
    {
        List<string> ret = new List<string>();

        if (names != null)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == null)
                {
                    continue;
                }

                ret.Add(AppendUserPrefix(names[i], prefix));
            }
        }

        return ret.ToArray();
    }

    public bool IsAllowed(string userRole, string aclRole)
    {
        return IsAllowed(new string[] { userRole }, new string[] { aclRole });
    }
    public bool IsAllowed(string userRole, string[] aclRoles)
    {
        return IsAllowed(new string[] { userRole }, aclRoles);
    }
    public bool IsAllowed(string[] userRoles, string[] aclRoles)
    {
        if (userRoles != null)
        {
            foreach (var userRole in userRoles)
            {
                foreach (var aclRole in aclRoles)
                {
                    if (aclRole == null)
                    {
                        continue;
                    }

                    string prefixWildcard = (userRole.Contains("::") ? userRole.Substring(0, userRole.IndexOf("::") + 2) + "%" : " ").ToLower();

                    if (aclRole == "*" || userRole.ToLower() == aclRole.ToLower() || prefixWildcard == aclRole.ToLower())
                    {
                        return true;
                    }

                    if (_options.AllowAccessControlAllowWildcards && aclRole.Contains("*"))
                    {
                        if (Regex.IsMatch(userRole.ToLower(), WildCardToRegular(aclRole.ToLower())))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    #region Wildcard Testing

    private String WildCardToRegular(String value)
    {
        // If you want to implement both "*" and "?"
        //return "^" + Regex.Escape(value).Replace("\\?", ".").Replace("\\*", ".*") + "$";

        // If you want to implement "*" only
        return "^" + Regex.Escape(value).Replace("\\*", ".*") + "$";
    }

    #endregion
}
