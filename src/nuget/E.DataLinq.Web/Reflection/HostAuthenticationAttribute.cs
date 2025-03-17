using System;

namespace E.DataLinq.Web.Reflection;

[Flags]
public enum HostAuthenticationTypes
{
    Any = 0,
    DataLinqEngine = 1,
    DataLinqAccessToken = 2
}

public class HostAuthenticationAttribute : Attribute
{
    public HostAuthenticationAttribute(HostAuthenticationTypes authenticationTypes)
    {
        this.AuthenticationType = authenticationTypes;
    }

    public HostAuthenticationTypes AuthenticationType { get; set; }
}
