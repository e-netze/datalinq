using E.DataLinq.Core.Services.Abstraction;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace E.DataLinq.Web.Services;

public class DataLinqInfoService
{
    public DataLinqInfoService(IEnumerable<IDataLinqCodeIdentityProvider> identityProviders)
    {
        CodeApi = identityProviders.Count() > 0;
    }

    public string Version =>
        Assembly
        .GetAssembly(typeof(DataLinq.Web.Models.SelectResult))
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        .InformationalVersion;

    public bool CodeApi { get; set; }
}
