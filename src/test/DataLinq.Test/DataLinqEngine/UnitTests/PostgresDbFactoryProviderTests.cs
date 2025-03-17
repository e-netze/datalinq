using E.DataLinq.Engine.Postgres;
using Npgsql;

namespace E.DataLinq.Test.DataLinqEngine.UnitTests;

[TestClass]
public class PostgresDbFactoryProviderTests
{
    private DbFactoryProvider _provider;

    [TestInitialize]
    public void Setup()
    {
        _provider = new DbFactoryProvider();
    }

    [TestMethod]
    public void GetFactory_ShouldReturnNpgsqlFactory()
    {
        var factory = _provider.GetFactory();
        Assert.IsNotNull(factory);
        Assert.IsInstanceOfType(factory, typeof(NpgsqlFactory));
    }

    [TestMethod]
    public void SupportsConnection_ShouldReturnTrueForPostgresPrefix()
    {
        var result = _provider.SupportsConnection("postgres://server/database");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void SupportsConnection_ShouldReturnFalseForOtherPrefix()
    {
        var result = _provider.SupportsConnection("mssql://server/database");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void RawConnectionString_ShouldRemovePrefix()
    {
        var connectionString = "postgres://server/database";
        var result = _provider.RawConnectionString(connectionString);
        Assert.AreNotEqual(connectionString, result);
    }
}
