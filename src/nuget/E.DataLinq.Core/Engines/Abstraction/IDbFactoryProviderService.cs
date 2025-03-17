namespace E.DataLinq.Core.Engines.Abstraction;

public interface IDbFactoryProviderService
{
    bool SupportsConnection(string connectionString);

    string RawConnectionString(string connectionString);

    System.Data.Common.DbProviderFactory GetFactory();
}
