using E.DataLinq.Code.Services;
using E.DataLinq.Core.Services.Abstraction;
using E.DataLinq.Core.Services.Crypto.Abstraction;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using System.Security.Principal;

namespace E.DataLinq.Test.DataLinqCode.UnitTests;

[TestClass]
public class DataLinqCodeServiceTests
{
    private Mock<IHttpContextAccessor>? _httpContextAccessorMock;
    private Mock<ICryptoService>? _cryptoServiceMock;
    private Mock<IOptions<DataLinqCodeOptions>>? _optionsMock;
    private Mock<IHostUrlHelper>? _urlHelperMock;
    private Mock<ICryptoService>? _cryptoMock;
    private DataLinqCodeIndentityService? _identityServiceMock;
    private DataLinqCodeService? _service;

    [TestInitialize]
    public void TestInitialize()
    {
        _cryptoServiceMock = new Mock<ICryptoService>();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();

        var httpContextMock = new Mock<HttpContext>();
        var claimsPrincipalMock = new Mock<ClaimsPrincipal>();

        var identityMock = new Mock<IIdentity>();
        identityMock.Setup(i => i.Name).Returns("TestUser");
        identityMock.Setup(i => i.IsAuthenticated).Returns(true);
        claimsPrincipalMock.Setup(x => x.Identity).Returns(identityMock.Object);
        claimsPrincipalMock.Setup(x => x.Claims).Returns(new List<Claim>
    {
        new Claim(DataLinqCodeIndentityService.InstanceIdClaimType, "0"),
        new Claim(DataLinqCodeIndentityService.AccessTokenClaimTyp, "validAccessToken")
    });

        httpContextMock.Setup(x => x.User).Returns(claimsPrincipalMock.Object);
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContextMock.Object);

        _optionsMock = new Mock<IOptions<DataLinqCodeOptions>>();
        var options = new DataLinqCodeOptions
        {
            DatalinqInstances = new[]
            {
            new DataLinqCodeOptions.DataLinqInstance
            {
                Name = "Local",
                CodeApiClientUrl = "http://localhost",
                LogoutUrl = "http://localhost/logout",
                LoginUrl = "http://localhost/login"
            }
        }
        };
        _optionsMock.Setup(o => o.Value).Returns(options);

        _cryptoMock = new Mock<ICryptoService>();
        _urlHelperMock = new Mock<IHostUrlHelper>();

        _identityServiceMock = new DataLinqCodeIndentityService(_httpContextAccessorMock.Object);
        _service = new DataLinqCodeService(/*new Mock<IHostUrlHelper>().Object*/_urlHelperMock.Object, _cryptoMock.Object, _identityServiceMock, _optionsMock.Object);
    }

    [TestMethod]
    public void Constructor_ShouldInitializeCorrectly_WhenIdentityDataIsValid()
    {
        var result = _service;

        Assert.IsNotNull(result);
        Assert.AreEqual("validAccessToken", result.AccessToken);
        Assert.AreEqual("Local", result.InstanceName);
        Assert.AreEqual("TestUser", result.UserDisplayName);
    }

    [TestMethod]
    public void Constructor_ShouldInitializeDatalinqInstances_WhenInstancesAreNull()
    {
        var options = new DataLinqCodeOptions
        {
            DatalinqInstances = null
        };
        _optionsMock!.Setup(o => o.Value).Returns(options);

        var service = new DataLinqCodeService(new Mock<IHostUrlHelper>().Object, _cryptoMock!.Object, _identityServiceMock, _optionsMock.Object);

        Assert.IsNotNull(options.DatalinqInstances);
        Assert.AreEqual(1, options.DatalinqInstances.Length);
        Assert.AreEqual("Local", options.DatalinqInstances[0].Name);
    }

    [TestMethod]
    public void Constructor_ShouldInitializeLoginAndLogoutUrlsCorrectly()
    {
        var options = new DataLinqCodeOptions
        {
            DatalinqInstances = null
        };
        _optionsMock!.Setup(o => o.Value).Returns(options);

        var service = new DataLinqCodeService(new Mock<IHostUrlHelper>().Object, _cryptoMock!.Object, _identityServiceMock, _optionsMock.Object);

        var instance = _optionsMock.Object.Value.DatalinqInstances[0];
        Assert.AreEqual("/DataLinqAuth?redirect=/DataLinqCode/Connect/", options.DatalinqInstances![0].LoginUrl);
        Assert.AreEqual("/DataLinqAuth/Logout?redirect={0}", options.DatalinqInstances[0].LogoutUrl);
    }
}
