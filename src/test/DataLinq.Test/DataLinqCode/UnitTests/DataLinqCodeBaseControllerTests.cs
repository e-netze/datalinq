using E.DataLinq.Code.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Text;

namespace E.DataLinq.Test.DataLinqCode.UnitTests;

[TestClass]
public class DataLinqCodeBaseControllerTests
{
    private class TestController : DataLinqCodeBaseController
    {
        public IActionResult CallJsonObject(object obj, bool pretty = false)
        {
            return JsonObject(obj, pretty);
        }

        public IActionResult CallJsonResultStream(string json)
        {
            return JsonResultStream(json);
        }

        public IActionResult CallBinaryResultStream(byte[] data, string contentType, string fileName = "")
        {
            return BinaryResultStream(data, contentType, fileName);
        }
    }

    [TestMethod]
    public void JsonObject_ShouldReturnJson_WhenCalledWithObject()
    {
        var controller = new TestController();
        var obj = new { Name = "Test", Age = 30 };
        var pretty = true;

        var mockHttpContext = new Mock<HttpContext>();
        var mockRequest = new Mock<HttpRequest>();
        var mockResponse = new Mock<HttpResponse>();

        var mockRequestHeaders = new Mock<IHeaderDictionary>();
        mockRequest.SetupGet(r => r.Headers).Returns(mockRequestHeaders.Object);

        var mockResponseHeaders = new Mock<IHeaderDictionary>();
        mockResponse.SetupGet(r => r.Headers).Returns(mockResponseHeaders.Object);

        mockHttpContext.SetupGet(x => x.Request).Returns(mockRequest.Object);
        mockHttpContext.SetupGet(x => x.Response).Returns(mockResponse.Object);

        controller.ControllerContext = new ControllerContext()
        {
            HttpContext = mockHttpContext.Object
        };

        var result = controller.CallJsonObject(obj, pretty) as FileContentResult;

        Assert.IsNotNull(result);
        var json = Encoding.UTF8.GetString((byte[])result.FileContents);
        Assert.IsTrue(json.Contains("\"Name\": \"Test\""));
        Assert.IsTrue(json.Contains("\"Age\": 30"));
    }

    [TestMethod]
    public void BinaryResultStream_ShouldReturnFileResult_WhenCalledWithData()
    {
        var controller = new TestController();
        var data = Encoding.UTF8.GetBytes("Test file content");
        var contentType = "application/pdf";
        var fileName = "testfile.pdf";

        var mockHttpContext = new Mock<HttpContext>();
        var mockRequest = new Mock<HttpRequest>();
        var mockResponse = new Mock<HttpResponse>();

        var mockRequestHeaders = new Mock<IHeaderDictionary>();
        mockRequest.SetupGet(r => r.Headers).Returns(mockRequestHeaders.Object);

        var mockResponseHeaders = new Mock<IHeaderDictionary>();
        mockResponse.SetupGet(r => r.Headers).Returns(mockResponseHeaders.Object);

        mockHttpContext.SetupGet(x => x.Request).Returns(mockRequest.Object);
        mockHttpContext.SetupGet(x => x.Response).Returns(mockResponse.Object);

        controller.ControllerContext = new ControllerContext()
        {
            HttpContext = mockHttpContext.Object
        };

        var result = controller.CallBinaryResultStream(data, contentType, fileName) as FileContentResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(contentType, result.ContentType);
        CollectionAssert.AreEqual(data, result.FileContents);
    }
}
