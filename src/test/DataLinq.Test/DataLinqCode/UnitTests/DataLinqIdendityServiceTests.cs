using E.DataLinq.Code.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;

namespace E.DataLinq.Test.DataLinqCode.UnitTests;

[TestClass]
public class DataLinqCodeIdentityServiceTests
{
    private Mock<IHttpContextAccessor>? _httpContextAccessorMock;
    private DefaultHttpContext? _httpContext;
    private DataLinqCodeIndentityService? _service;

    [TestInitialize]
    public void Setup()
    {
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _httpContext = new DefaultHttpContext();
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(_httpContext);
        _service = new DataLinqCodeIndentityService(_httpContextAccessorMock.Object);
    }

    [TestMethod]
    public void IdentityData_ShouldReturnCorrectValues_WhenUserIsAuthenticated()
    {
        var claims = new List<Claim>
    {
        new Claim(DataLinqCodeIndentityService.InstanceIdClaimType, "123"),
        new Claim(DataLinqCodeIndentityService.AccessTokenClaimTyp, "test_token"),
        new Claim(ClaimTypes.Name, "TestUser")
    };
        var identity = new ClaimsIdentity(claims, "mock");
        _httpContext!.User = new ClaimsPrincipal(identity);

        var result = _service!.IdentityData();

        Assert.AreEqual(123, result.id);
        Assert.AreEqual("TestUser", result.userDisplayName);
        Assert.AreEqual("test_token", result.accessToken);
    }

    [TestMethod]
    public void IdentityData_ShouldReturnNulls_WhenUserIsNotAuthenticated()
    {
        _httpContext!.User = new ClaimsPrincipal(new ClaimsIdentity());

        var result = _service!.IdentityData();

        Assert.IsNull(result.id);
        Assert.IsNull(result.userDisplayName);
        Assert.IsNull(result.accessToken);
    }

    [TestMethod]
    public void IdentityData_ShouldReturnNulls_WhenClaimsAreMissing()
    {
        var claims = new List<Claim> { new Claim(ClaimTypes.Name, "TestUser") };
        var identity = new ClaimsIdentity(claims, "mock");
        _httpContext!.User = new ClaimsPrincipal(identity);

        var result = _service!.IdentityData();

        Assert.IsNull(result.id);
        Assert.IsNull(result.userDisplayName);
        Assert.IsNull(result.accessToken);
    }


    [TestMethod]
    public void IdentityData_ShouldReturnNulls_WhenInstanceIdIsInvalid()
    {
        var claims = new List<Claim>
    {
        new Claim(DataLinqCodeIndentityService.InstanceIdClaimType, "invalid_number"),
        new Claim(DataLinqCodeIndentityService.AccessTokenClaimTyp, "test_token"),
        new Claim(ClaimTypes.Name, "TestUser")
    };
        var identity = new ClaimsIdentity(claims, "mock");
        _httpContext!.User = new ClaimsPrincipal(identity);

        var result = _service!.IdentityData();

        Assert.IsNull(result.id);
        Assert.IsNull(result.userDisplayName);
        Assert.IsNull(result.accessToken);
    }
}
