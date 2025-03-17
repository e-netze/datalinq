using E.DataLinq.Code.Extensions;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Buffers;
using Moq;
using System.Linq.Expressions;
using System.Text.Encodings.Web;

namespace E.DataLinq.Test.DataLinqCode.UnitTests;

[TestClass]
public class HtmlExtensionsTests
{
    private enum TestEnum
    {
        FirstOption,
        SecondOption
    }

    private class SelectItem
    {
        public string Text { get; set; }
        public string Value { get; set; }
    }

    [TestMethod]
    public void EnumToSelectList_ShouldReturnValidList()
    {
        var result = HtmlExtensions.EnumToSelectList<SelectItem, TestEnum>(default).ToList();

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("FirstOption", result[0].Text);
        Assert.AreEqual("FirstOption", result[0].Value);
        Assert.AreEqual("SecondOption", result[1].Text);
        Assert.AreEqual("SecondOption", result[1].Value);
    }

    [TestMethod]
    public void DictToSelectList_ShouldReturnValidList()
    {
        var dict = new Dictionary<int, string>
        {
            { 1, "One" },
            { 2, "Two" }
        };

        var result = HtmlExtensions.DictToSelectList<SelectItem>(dict).ToList();

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("One", result[0].Text);
        Assert.AreEqual("1", result[0].Value);
        Assert.AreEqual("Two", result[1].Text);
        Assert.AreEqual("2", result[1].Value);
    }

    [TestMethod]
    public void DescriptionFor_ShouldReturnValidHtmlString()
    {
        var model = new TestModel { Name = "Test" };
        var expression = (Expression<Func<TestModel, string>>)(m => m.Name);

        var htmlGeneratorMock = new Mock<IHtmlGenerator>();
        var compositeViewEngineMock = new Mock<ICompositeViewEngine>();
        var modelMetadataProviderMock = new Mock<IModelMetadataProvider>();
        var viewBufferScopeMock = new Mock<IViewBufferScope>();
        var htmlEncoderMock = new Mock<HtmlEncoder>();
        var urlEncoderMock = new Mock<UrlEncoder>();

        var modelExpressionProviderMock = new Mock<ModelExpressionProvider>(modelMetadataProviderMock.Object);

        var htmlHelper = new HtmlHelper<TestModel>(
            htmlGeneratorMock.Object,
            compositeViewEngineMock.Object,
            modelMetadataProviderMock.Object,
            viewBufferScopeMock.Object,
            htmlEncoderMock.Object,
            urlEncoderMock.Object,
            modelExpressionProviderMock.Object);

        var result = HtmlExtensions.DescriptionFor(htmlHelper, expression) as HtmlString;

        Assert.IsNotNull(result, "Expected HTML string, but result was null.");
        Assert.IsTrue(result.Value.Contains("description"), "HTML string does not contain 'description' element.");
    }

    public class TestModel
    {
        [System.ComponentModel.Description("This is a test name")]
        public string Name { get; set; }
    }
}
