using E.DataLinq.Engine.SqlServer;
using System.Data.SqlClient;

namespace E.DataLinq.Test.DataLinqEngine.UnitTests;

[TestClass]
public class SqlClientDbFactoryProviderTests
{
    private SqlClientDbFactoryProvider _provider;

    [TestInitialize]
    public void Setup()
    {
        _provider = new SqlClientDbFactoryProvider();
    }

    [TestMethod]
    public void GetFactory_ShouldReturnSqlClientFactory()
    {
        var factory = _provider.GetFactory();
        Assert.IsNotNull(factory);
        Assert.IsInstanceOfType(factory, typeof(SqlClientFactory));
    }

    [TestMethod]
    public void SupportsConnection_ShouldReturnTrueForSqlServerPrefix()
    {
        var result = _provider.SupportsConnection("sqlserver-legacy://server/database");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void SupportsConnection_ShouldReturnTrueForSqlPrefix()
    {
        var result = _provider.SupportsConnection("sql-legacy://server/database");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void SupportsConnection_ShouldReturnFalseForOtherPrefix()
    {
        var result = _provider.SupportsConnection("postgres://server/database");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void RawConnectionString_ShouldRemovePrefix()
    {
        var connectionString = "sqlserver://server/database";
        var result = _provider.RawConnectionString(connectionString);
        Assert.AreNotEqual(connectionString, result);
    }
}
