using E.DataLinq.Engine.SQLite;
using System.Data.SQLite;

namespace E.DataLinq.Test.DataLinqEngine.UnitTests;

[TestClass]
public class SQLiteDbFactoryProviderTests
{
    private DbFactoryProvider _provider;

    [TestInitialize]
    public void Setup()
    {
        _provider = new DbFactoryProvider();
    }

    [TestMethod]
    public void GetFactory_ShouldReturnSQLiteFactory()
    {
        var factory = _provider.GetFactory();
        Assert.IsNotNull(factory);
        Assert.IsInstanceOfType(factory, typeof(SQLiteFactory));
    }

    [TestMethod]
    public void SupportsConnection_ShouldReturnTrueForSQLitePrefix()
    {
        var result = _provider.SupportsConnection("sqlite://database.db");
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
        var connectionString = "sqlite://database.db";
        var result = _provider.RawConnectionString(connectionString);
        Assert.AreNotEqual(connectionString, result);
    }
}
