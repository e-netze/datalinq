using System.Collections.Generic;

namespace E.DataLinq.Core.Models.Authentication;

public class DataLinqCodeIdentity
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public IEnumerable<string> Roles { get; set; }
}
