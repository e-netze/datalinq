using E.DataLinq.Core.Engines.Abstraction;
using E.DataLinq.Core.Extensions;
using System;
using System.Data.Common;

namespace E.DataLinq.Engine.OracleClient;

public class DbFactoryProvider : IDbFactoryProviderService
{
    public DbProviderFactory GetFactory()
    {
        return Oracle.ManagedDataAccess.Client.OracleClientFactory.Instance;
    }

    public string RawConnectionString(string connectionString)
    {
        return connectionString.RemovePrefix();
    }

    public bool SupportsConnection(string connectionString)
    {
        var prefix = connectionString.GetPrefix();

        return "oracle".Equals(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
