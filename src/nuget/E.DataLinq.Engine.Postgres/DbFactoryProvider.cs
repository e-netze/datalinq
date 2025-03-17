using E.DataLinq.Core.Engines.Abstraction;
using E.DataLinq.Core.Extensions;
using System;
using System.Data.Common;

namespace E.DataLinq.Engine.Postgres;

public class DbFactoryProvider : IDbFactoryProviderService
{
    public DbProviderFactory GetFactory()
    {
        return Npgsql.NpgsqlFactory.Instance;
    }

    public string RawConnectionString(string connectionString)
    {
        return connectionString.RemovePrefix();
    }

    public bool SupportsConnection(string connectionString)
    {
        var prefix = connectionString.GetPrefix();

        return "postgres".Equals(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
