using E.DataLinq.Engine.MsSqlServer;
using Microsoft.Data.SqlClient;

namespace E.DataLinq.Test.DataLinqEngine.UnitTests;

[TestClass]
public class MsSqlClientDbFactoryProviderTests
{
    private MsSqlClientDbFactoryProvider _provider;

    [TestInitialize]
    public void Setup()
    {
        _provider = new MsSqlClientDbFactoryProvider(null);
    }

    [TestMethod]
    public void GetFactory_ShouldReturnSqlClientFactory()
    {
        var factory = _provider.GetFactory();
        Assert.IsNotNull(factory);
        Assert.IsInstanceOfType(factory, typeof(SqlClientFactory));
    }

    [TestMethod]
    public void SupportsConnection_ShouldReturnTrueForMssqlPrefix()
    {
        var result = _provider.SupportsConnection("mssql://server/database");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void SupportsConnection_ShouldReturnFalseForOtherPrefix()
    {
        var result = _provider.SupportsConnection("oracle://server/database");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void RawConnectionString_ShouldRemovePrefix()
    {
        var connectionString = "mssql://server/database";
        var result = _provider.RawConnectionString(connectionString);
        Assert.AreNotEqual(connectionString, result);
    }
}
