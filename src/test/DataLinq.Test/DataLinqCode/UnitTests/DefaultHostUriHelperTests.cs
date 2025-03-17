using E.DataLinq.Code.Services;
using Microsoft.AspNetCore.Http;
using Moq;

namespace E.DataLinq.Test.DataLinqCode.UnitTests;

[TestClass]
public class DefaultHostUrlHelperTests
{
    private Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private Mock<HttpContext> _httpContextMock;
    private Mock<HttpRequest> _httpRequestMock;

    [TestInitialize]
    public void TestInitialize()
    {
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _httpContextMock = new Mock<HttpContext>();
        _httpRequestMock = new Mock<HttpRequest>();
    }

    [TestMethod]
    public void HostAppRootUrl_ShouldReturnCorrectUrl_WhenHttpContextIsValid()
    {
        _httpRequestMock.Setup(r => r.Scheme).Returns("https");
        _httpRequestMock.Setup(r => r.Host).Returns(new HostString("example.com"));
        _httpRequestMock.Setup(r => r.PathBase).Returns(new PathString("/app"));

        _httpContextMock.Setup(c => c.Request).Returns(_httpRequestMock.Object);
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns(_httpContextMock.Object);

        var helper = new DefaultHostUrlHelper(_httpContextAccessorMock.Object);

        var result = helper.HostAppRootUrl();

        Assert.AreEqual("https://example.com/app", result);
    }

    [TestMethod]
    public void HostAppRootUrl_ShouldReturnBaseUrl_WhenPathBaseIsEmpty()
    {
        _httpRequestMock.Setup(r => r.Scheme).Returns("http");
        _httpRequestMock.Setup(r => r.Host).Returns(new HostString("localhost:5000"));
        _httpRequestMock.Setup(r => r.PathBase).Returns(PathString.Empty);

        _httpContextMock.Setup(c => c.Request).Returns(_httpRequestMock.Object);
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns(_httpContextMock.Object);

        var helper = new DefaultHostUrlHelper(_httpContextAccessorMock.Object);

        var result = helper.HostAppRootUrl();

        Assert.AreEqual("http://localhost:5000", result);
    }

    [TestMethod]
    public void HostAppRootUrl_ShouldThrowNullReferenceException_WhenHttpContextIsNull()
    {
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns((HttpContext)null);

        Assert.ThrowsException<NullReferenceException>(() =>
        {
            var helper = new DefaultHostUrlHelper(_httpContextAccessorMock.Object);
            _ = helper.HostAppRootUrl();
        });
    }

    [TestMethod]
    public void HostAppRootUrl_ShouldThrowNullReferenceException_WhenHttpRequestIsNull()
    {
        _httpContextMock.Setup(c => c.Request).Returns((HttpRequest)null);
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns(_httpContextMock.Object);

        Assert.ThrowsException<NullReferenceException>(() =>
        {
            var helper = new DefaultHostUrlHelper(_httpContextAccessorMock.Object);
            _ = helper.HostAppRootUrl();
        });
    }
}
