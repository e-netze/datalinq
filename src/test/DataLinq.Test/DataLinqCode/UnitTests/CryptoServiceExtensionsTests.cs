using E.DataLinq.Code.Extensions;
using E.DataLinq.Core.Services.Crypto;
using E.DataLinq.Core.Services.Crypto.Abstraction;
using Moq;

namespace E.DataLinq.Test.DataLinqCode.UnitTests;

[TestClass]
public class CryptoServiceExtensionsTests
{
    private Mock<ICryptoService> _cryptoServiceMock;

    [TestInitialize]
    public void TestInitialize()
    {
        _cryptoServiceMock = new Mock<ICryptoService>();
    }

    [TestMethod]
    public void ToSessionString_ShouldReturnEncryptedString_WhenValidDataProvided()
    {
        var fakeEncryptedString = "encryptedData";
        _cryptoServiceMock
            .Setup(c => c.EncryptTextDefault(It.IsAny<string>(), CryptoResultStringType.Hex))
            .Returns(fakeEncryptedString);

        var crypto = _cryptoServiceMock.Object;
        var data = new string[] { "user123", "token456" };

        var result = crypto.ToSessionString(data);

        Assert.AreEqual(fakeEncryptedString, result);
        _cryptoServiceMock.Verify(c => c.EncryptTextDefault(It.IsAny<string>(), CryptoResultStringType.Hex), Times.Once);
    }

    [TestMethod]
    public void GetSessionData_ShouldReturnDecryptedData_WhenValidSessionStringProvided()
    {
        var decryptedString = "123e4567-e89b-12d3-a456-426614174000:user123$token456";
        _cryptoServiceMock
            .Setup(c => c.DecryptTextDefault(It.IsAny<string>()))
            .Returns(decryptedString);

        var crypto = _cryptoServiceMock.Object;
        var sessionString = "encryptedData";

        var result = crypto.GetSessionData(sessionString);

        CollectionAssert.AreEqual(new string[] { "user123", "token456" }, result);
        _cryptoServiceMock.Verify(c => c.DecryptTextDefault(sessionString), Times.Once);
    }

    [TestMethod]
    public void GetSessionData_ShouldReturnEmptyArray_WhenSessionStringIsNullOrEmpty()
    {
        var crypto = _cryptoServiceMock.Object;

        var result1 = crypto.GetSessionData(null);
        var result2 = crypto.GetSessionData("");

        CollectionAssert.AreEqual(Array.Empty<string>(), result1);
        CollectionAssert.AreEqual(Array.Empty<string>(), result2);
    }

    [TestMethod]
    public void GetSessionData_ShouldThrowException_WhenDecryptedStringIsInvalid()
    {
        var invalidDecryptedString = "InvalidSessionDataWithoutColon";
        _cryptoServiceMock
            .Setup(c => c.DecryptTextDefault(It.IsAny<string>()))
            .Returns(invalidDecryptedString);

        var crypto = _cryptoServiceMock.Object;
        var sessionString = "encryptedData";

        Assert.ThrowsException<Exception>(() => crypto.GetSessionData(sessionString), "Invalid session string");
    }
}
