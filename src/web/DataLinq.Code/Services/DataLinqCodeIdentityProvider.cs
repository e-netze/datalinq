using E.DataLinq.Core.Models.Authentication;
using E.DataLinq.Core.Services.Abstraction;
using Microsoft.Extensions.Primitives;

namespace E.DataLinq.Services;

class DataLinqCodeIdentityProvider : IDataLinqCodeIdentityProvider
{
    private readonly IConfiguration _config;
    public DataLinqCodeIdentityProvider(IConfiguration config)
    {
        _config = config;
    }

    #region IDataLinqIdentityProvider

    public DataLinqCodeIdentity? TryGetIdentity(string name, string password)
    {
        var clients = _config.GetSection("DataLinq:Code:Clients");
        int index = 0;

        while (true)
        {
            var client = clients.GetSection((index++).ToString());
            if (client["Id"] == null)
            {
                break;
            }

            if (name.Equals(client["Name"], StringComparison.OrdinalIgnoreCase) &&
                password == client["Password"])
            {
                return new DataLinqCodeIdentity()
                {
                    Id = client["Id"],
                    Name = client["Name"],
                    Roles = new[] { $"datalinq-code({client["EndPointParameters"]})" }
                };
            }
        }

        return null;
    }

    public DataLinqCodeIdentity? TryGetIdentity(IEnumerable<KeyValuePair<string, StringValues>> parameters)
    {
        return null;
    }

    #endregion
}
