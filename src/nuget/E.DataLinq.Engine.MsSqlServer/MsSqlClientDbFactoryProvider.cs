using E.DataLinq.Core.Engines.Abstraction;
using E.DataLinq.Core.Extensions;
using System;
using System.Data.Common;

namespace E.DataLinq.Engine.MsSqlServer;

public class MsSqlClientDbFactoryProvider : IDbFactoryProviderService
{
    public DbProviderFactory GetFactory()
    {
        return Microsoft.Data.SqlClient.SqlClientFactory.Instance;
    }

    public string RawConnectionString(string connectionString)
    {
        return connectionString.RemovePrefix();
    }

    public bool SupportsConnection(string connectionString)
    {
        var prefix = connectionString.GetPrefix();

        return 
            "mssql".Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
            "sqlserver".Equals(prefix, StringComparison.OrdinalIgnoreCase) || 
            "sql".Equals(prefix, StringComparison.OrdinalIgnoreCase);
    }

    // Ensure Microsoft.SqlServer.Types is added target...
    public void LoadSqlServerTypes()
    {
        var nullGemetry = Microsoft.SqlServer.Types.SqlGeography.Null;
    }
}
