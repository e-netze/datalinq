using E.DataLinq.Core.Engines.Abstraction;
using E.DataLinq.Core.Extensions;
using System;
using System.Data.Common;

namespace E.DataLinq.Engine.SqlServer;

public class SqlClientDbFactoryProvider : IDbFactoryProviderService
{
    public DbProviderFactory GetFactory()
    {
#pragma warning disable CS0618  //  System.Data.SqlClient is obsolete
        return System.Data.SqlClient.SqlClientFactory.Instance;
#pragma warning restore CS0618
    }

    public string RawConnectionString(string connectionString)
    {
        return connectionString.RemovePrefix();
    }

    public bool SupportsConnection(string connectionString)
    {
        var prefix = connectionString.GetPrefix();

        return "sqlserver-legacy".Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
               "sql-legacy".Equals(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
