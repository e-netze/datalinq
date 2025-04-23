namespace E.DataLinq.Core.Engines.Abstraction;

public interface IDbFactoryProviderConnectionStringModifyService
{
    string ModifyConnectionString(string dbPrefix, string connectionString);
}
