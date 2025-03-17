using E.DataLinq.Core.Engines.Abstraction;
using E.DataLinq.Core.Extensions;
using System;
using System.Data.Common;

namespace E.DataLinq.Engine.SQLite;

public class DbFactoryProvider : IDbFactoryProviderService
{
    public DbProviderFactory GetFactory()
    {
        return System.Data.SQLite.SQLiteFactory.Instance;
    }

    public string RawConnectionString(string connectionString)
    {
        return connectionString.RemovePrefix();
    }

    public bool SupportsConnection(string connectionString)
    {
        var prefix = connectionString.GetPrefix();

        return "sqlite".Equals(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
