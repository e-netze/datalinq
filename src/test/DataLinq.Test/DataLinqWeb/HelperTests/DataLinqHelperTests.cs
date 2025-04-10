using E.DataLinq.Core;
using E.DataLinq.Web.Razor;
using E.DataLinq.Web.Services;
using E.DataLinq.Web.Services.Abstraction;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Dynamic;
using System.Text.RegularExpressions;

namespace DataLinq.Test.DataLinqWeb.HelperTests;

[TestClass]
public class DataLinqHelperTests
{
    private readonly Mock<IRazorCompileEngineService> _razorMock;
    private readonly Mock<IDataLinqUser> _uiMock;
    private readonly Mock<DataLinqService> _dataLinqServiceMock;
    private readonly Mock<HttpContext> _httpContextMock;
    private static DataLinqHelperClassic _classicHelper;
    private static DataLinqHelper _newHelper;

    private static string patternGuid = @"[a-fA-F0-9]{8}-([a-fA-F0-9]{4}-){3}[a-fA-F0-9]{12}";

    private static object HTML_ATTRIBUTES = new
    {
        @class = "container",
        style = "color: red; font-size: 16px;",
        title = "This is a tooltip",
        dataCustom = "customValue"
    };

    private static IDictionary<string, object>[] RECORD =
    {
        new Dictionary<string, object>
        {
            { "Id", 1 },
            { "Name", "Alice" },
            { "Age", 25 }
        },
        new Dictionary<string, object>
        {
            { "Id", 2 },
            { "Name", "Bob" },
            { "Age", 30 }
        }
    };


    dynamic OBJ = new ExpandoObject();

    public DataLinqHelperTests()
    {
        _razorMock = new Mock<IRazorCompileEngineService>();
        _uiMock = new Mock<IDataLinqUser>();
        _httpContextMock = new Mock<HttpContext>();

        _razorMock.Setup(r => r.RawString(It.IsAny<string>())).Returns((string input) => input);

        _classicHelper = new DataLinqHelperClassic(_httpContextMock.Object, null, _razorMock.Object, _uiMock.Object);
        _newHelper = new DataLinqHelper(_httpContextMock.Object, null, _razorMock.Object, _uiMock.Object);

        OBJ.Id = "Yes";
        OBJ.Name = "Max";
        OBJ.Age = 24;
    }

    [TestMethod]
    public void JsFetchData_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.JsFetchData("url1", "callback", "filter");
        var newResult = _newHelper.JsFetchData("url1", "callback", "filter");

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void RecordsToJs_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.RecordsToJs(RECORD, "callback");
        var newResult = _newHelper.RecordsToJs(RECORD, "callback");

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void IncludeView_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.IncludeView("id", "filter", "order");
        var newResult = _newHelper.IncludeView("id", "filter", "order");

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void IncludeClickView_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.IncludeClickView("id", "text", "filter", "order");
        var newResult = _newHelper.IncludeClickView("id", "text", "filter", "order");

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void RefreshViewClick_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.RefreshViewClick("label", HTML_ATTRIBUTES);
        var newResult = _newHelper.RefreshViewClick("label", HTML_ATTRIBUTES);

        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void RefreshViewTicker_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.RefreshViewTicker("Test Test",99,HTML_ATTRIBUTES);
        var newResult = _newHelper.RefreshViewTicker("Test Test", 99, HTML_ATTRIBUTES);

        // Assert
        Assert.AreEqual(RemoveIdAttributes(classicResult.ToString()), RemoveIdAttributes(newResult.ToString()));
    }

    [TestMethod]
    public void SortView_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.SortView("label", new string[] { "one", "two" }, HTML_ATTRIBUTES);
        var newResult = _newHelper.SortView("label", new string[] { "one", "two" }, HTML_ATTRIBUTES);

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void FilterView_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.FilterView("label", new string[] { "one", "two" }, HTML_ATTRIBUTES);
        var newResult = _newHelper.FilterView("label", new string[] { "one", "two" }, HTML_ATTRIBUTES);

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void FilterViewDict_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        var dict = new Dictionary<string, object>()
        {
            { "ort", new { displayname = "Ort/Gemeinde" } },
            { "datum", new { displayname = "Datum", dataType = DataType.Date } },
            { "str", new { displayname = "Strasse", source = "endpoint-id@query-id", valueField = "STR", nameField = "STR_LANGTEXT", prependEmpty = true } }
        };

        // Act
        var classicResult = _classicHelper.FilterView("label", dict, HTML_ATTRIBUTES);
        var newResult = _newHelper.FilterView("label", dict, HTML_ATTRIBUTES);

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void UpdateFilterButton_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.UpdateFilterButton("filterName", new { one = "two" }, "buttonText", "filterid", HTML_ATTRIBUTES);
        var newResult = _newHelper.UpdateFilterButton("filterName", new { one = "two" }, "buttonText", "filterid", HTML_ATTRIBUTES);

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void ExportView_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.ExportView("lable", HTML_ATTRIBUTES);
        var newResult = _newHelper.ExportView("lable", HTML_ATTRIBUTES);

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void Table_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        IEnumerable<IDictionary<string, object>> records = new List<IDictionary<string, object>>()
        {
            new Dictionary<string, object>()
            {
                { "ID", 1 },
                { "GNR_KEY", "63113    16    4" },
                { "Eigentuemer", "Stadt Graz" },
                { "Anteil", "1/1" },
                { "Timestamp", new DateTime(2022, 3, 8) }
            },
            new Dictionary<string, object>()
            {
                { "ID", 3 },
                { "GNR_KEY", "63113  2157    5" },
                { "Eigentuemer", "AFZELIA GmbH & Co KG (543462i) (FB 543462i)" },
                { "Anteil", "1/1" },
                { "Timestamp", new DateTime(2024, 2, 1) }
            },
            new Dictionary<string, object>()
            {
                { "ID", 4 },
                { "GNR_KEY", "63113     2   13" },
                { "Eigentuemer", "Stadt Graz" },
                { "Anteil", "1/1" },
                { "Timestamp", new DateTime(2022, 3, 8) }
            }
        };
        // Act
        var classicResult = _classicHelper.Table(records,null, HTML_ATTRIBUTES);
        var newResult = _newHelper.Table(records,null, HTML_ATTRIBUTES);

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void BeginForm_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.BeginForm("id", HTML_ATTRIBUTES);
        var newResult = _newHelper.BeginForm("id", HTML_ATTRIBUTES);

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void EndForm_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.EndForm("submit", "cancle");
        var newResult = _newHelper.EndForm("submit", "cancle");

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void OpenViewInDialog_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.OpenViewInDialog("id", new { one = "two" }, HTML_ATTRIBUTES, "button", new { three = "four" });
        var newResult = _newHelper.OpenViewInDialog("id", new { one = "two" }, HTML_ATTRIBUTES, "button", new { three = "four" });

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void ExecuteNonQuery_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.ExecuteNonQuery("id", new { one = "two" }, HTML_ATTRIBUTES, "button", new { three = "four" });
        var newResult = _newHelper.ExecuteNonQuery("id", new { one = "two" }, HTML_ATTRIBUTES, "button", new { three = "four" });

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void IncludeCombo_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.IncludeCombo("id", "url", "value", "name", new { name = "value" });
        var newResult = _newHelper.IncludeCombo("id", "url", "value", "name", new { name = "value" });

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void ComboFor_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.ComboFor(OBJ, "name", HTML_ATTRIBUTES, new { source = new string[] { "Yes", "No", "Maybe" } }, new { value = "text" });
        var newResult = _newHelper.ComboFor(OBJ, "name", HTML_ATTRIBUTES, new { source = new string[] { "Yes", "No", "Maybe" } }, new { value = "text" });

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void RadioFor_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.RadioFor(OBJ, "Id", HTML_ATTRIBUTES, new { source = new string[] { "Yes", "No", "Maybe" } }, "test");
        var newResult = _newHelper.RadioFor(OBJ, "Id", HTML_ATTRIBUTES, new { source = new string[] { "Yes", "No", "Maybe" } }, "test");

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void TextFor_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.TextFor(OBJ, "name", HTML_ATTRIBUTES, new { source = "abc" });
        var newResult = _newHelper.TextFor(OBJ, "name", HTML_ATTRIBUTES, new { source = "abc" });

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void CheckboxFor_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.CheckboxFor(OBJ, "name", HTML_ATTRIBUTES, true);
        var newResult = _newHelper.CheckboxFor(OBJ, "name", HTML_ATTRIBUTES,  true);

        // Assert
        Assert.AreEqual(Regex.Replace(classicResult,patternGuid,""), Regex.Replace(newResult,patternGuid,""));
    }

    [TestMethod]
    public void TextboxFor_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.TextboxFor(OBJ, "name", HTML_ATTRIBUTES, new { value = "abc" });
        var newResult = _newHelper.TextboxFor(OBJ, "name", HTML_ATTRIBUTES, new { value = "abc" });

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void HiddenFor_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.HiddenFor(OBJ, "name", HTML_ATTRIBUTES, new { value = "abc" });
        var newResult = _newHelper.HiddenFor(OBJ, "name", HTML_ATTRIBUTES, new { value = "abc" });

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void LabelFor_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.LabelFor("label", "name", HTML_ATTRIBUTES);
        var newResult = _newHelper.LabelFor("label", "name", HTML_ATTRIBUTES);

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void CopyButton_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.CopyButton("id","baseText",HTML_ATTRIBUTES);
        var newResult = _newHelper.CopyButton("id", "baseText", HTML_ATTRIBUTES);

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void ExecuteScalar_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.ExecuteScalar(HTML_ATTRIBUTES, new { source = "endpoint-id@query-id?id=2", nameField = "NAME" },"span", "test");
        var newResult = _newHelper.ExecuteScalar(HTML_ATTRIBUTES, new { source = "endpoint-id@query-id?id=2", nameField = "NAME" }, "span", "test");

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void StatisticsCount_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.StatisticsCount(RECORD,"Label",HTML_ATTRIBUTES);
        var newResult = _newHelper.StatisticsCount(RECORD, "Label", HTML_ATTRIBUTES);

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void Chart_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.Chart(ChartType.Bar,"jsValueVariable","Label",HTML_ATTRIBUTES, ["123","124","125"],"jsDataSetVariable");
        var newResult = _newHelper.Chart(ChartType.Bar, "jsValueVariable", "Label", HTML_ATTRIBUTES, ["123", "124", "125"], "jsDataSetVariable");

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    [TestMethod]
    public void ResponsiveSwitcher_ShouldReturnSameResult_ForClassicAndNewImplementation()
    {
        // Act
        var classicResult = _classicHelper.ResponsiveSwitcher();
        var newResult = _newHelper.ResponsiveSwitcher();

        // Assert
        Assert.AreEqual(classicResult, newResult);
    }

    private static string RemoveIdAttributes(string html)
    {
        string idPattern = @"id='[^']*'";
        string forPattern = @"for='[^']*'";

        html = Regex.Replace(html, idPattern, string.Empty); 
        html = Regex.Replace(html, forPattern, string.Empty); 

        return html;
    }
}
