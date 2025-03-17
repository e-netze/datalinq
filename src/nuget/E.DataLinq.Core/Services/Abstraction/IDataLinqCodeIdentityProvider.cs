using E.DataLinq.Core.Models.Authentication;
using Microsoft.Extensions.Primitives;
using System.Collections.Generic;

namespace E.DataLinq.Core.Services.Abstraction;

public interface IDataLinqCodeIdentityProvider
{
    DataLinqCodeIdentity TryGetIdentity(IEnumerable<KeyValuePair<string, StringValues>> parameters);
    DataLinqCodeIdentity TryGetIdentity(string name, string password);
}
