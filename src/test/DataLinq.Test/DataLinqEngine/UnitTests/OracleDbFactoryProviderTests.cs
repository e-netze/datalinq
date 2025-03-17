using E.DataLinq.Engine.OracleClient;
using Oracle.ManagedDataAccess.Client;

namespace E.DataLinq.Test.DataLinqEngine.UnitTests;

[TestClass]
public class OracleDbFactoryProviderTests
{
    private DbFactoryProvider _provider;

    [TestInitialize]
    public void Setup()
    {
        _provider = new DbFactoryProvider();
    }

    [TestMethod]
    public void GetFactory_ShouldReturnOracleClientFactory()
    {
        var factory = _provider.GetFactory();
        Assert.IsNotNull(factory);
        Assert.IsInstanceOfType(factory, typeof(OracleClientFactory));
    }

    [TestMethod]
    public void SupportsConnection_ShouldReturnTrueForOraclePrefix()
    {
        var result = _provider.SupportsConnection("oracle://server/database");
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
        var connectionString = "oracle://server/database";
        var result = _provider.RawConnectionString(connectionString);
        Assert.AreNotEqual(connectionString, result);
    }
}
