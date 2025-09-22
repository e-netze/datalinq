using E.DataLinq.Core;
using E.DataLinq.Core.Models;
using E.DataLinq.Web.Api.Client;
using Microsoft.SemanticKernel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace E.DataLinq.Code.Services.Plugins;

public class DataLinqFunctionsPlugin
{
    private readonly CodeApiClient _client;
    private readonly DataLinqCodeService _dataLinqCode;

    public DataLinqFunctionsPlugin(DataLinqCodeService dataLinqCode)
    {
        _dataLinqCode = dataLinqCode;
        _client = _dataLinqCode.ApiClient;
    }

public record DataLinqFunctionInfo(string Name, string ShortDescription);

    [KernelFunction("get_all_datalinq_functions")]
    [Description("Gets all of the datalinq function names and short descriptions")]
    public List<DataLinqFunctionInfo> GetAllDataLinqFunctions()
    {
        return new() {
                new("Table", "Creates an HTML table."),
                new("FilterView", "Adds a filter control for a table."),
                new("SortView", "Adds a sort control for a table."),
                new("BeginForm", "Marks the beginning of an HTML form."),
                new("EndForm", "Marks the end of an HTML form."),
                new("TextFor", "Creates a text input field."),
                new("LabelFor", "Creates a label for an input field."),
                new("ComboFor", "Creates a dropdown (select) input field."),
                new("HiddenFor", "Creates a hidden input field."),
                new("CheckboxFor", "Creates a checkbox input field."),
                new("RadioFor", "Creates a radio button input field."),
                new("TextboxFor", "Creates a multi-line text area input field."),
                new("ExportView", "Creates a button to export a filtered/sorted table as CSV."),
                new("IncludeView", "Includes a child view by ID into the parent view."),
                new("IncludeClickView", "Creates a button that includes a child view by ID into the parent view when clicked."),
                new("OpenViewInDialog", "Creates a button that opens a modal dialog with a child view by ID when clicked."),
                new("ExecuteNonQuery", "Creates a button that runs a query without returning results."),
                new("ExecuteScalar", "Runs a query and returns a single value (scalar)."),
                new("GetCurrentUsername", "Gets the current username."),
                new("GetRecordsAsync", "Runs a query and returns the data as an object."),
                new("GetRequestHeaderValue", "Gets the value of a specific request header."),
                new("JsFetchData", "Runs a query and passes the data into a JavaScript callback function."),
                new("RecordsToJs", "Sends already loaded data into a JavaScript callback function."),
                new("RefreshViewClick", "Creates a button that refreshes the current view."),
                new("RefreshViewTicker", "Creates a timer that refreshes the view on countdown (can be disabled)."),
                new("ResponsiveSwitch", "Creates a button that toggles the 'responsive' CSS class on all elements."),
                new("UpdateFilterButton", "Creates a button that updates a value in the FilterView control."),
                new("UrlEncode", "Encodes a string for safe use in a URL."),
                new("StatisticsCount", "Creates a JavaScript variable with the number of records."),
                new("StatisticsGroupBy", "Creates a JavaScript variable with grouped data by a field (for charts)."),
                new("StatisticsGroupByDerived", "Creates a JavaScript variable with grouped data and computed values by category (for charts)."),
                new("StatisticsGroupByTime", "Creates a JavaScript variable with grouped data by a datetime field (for charts)."),
                new("StatisticsTime", "Creates a JavaScript variable with datetime-based values (for charts)."),
                new("Chart", "Creates a chart from data produced by a Statistics function."),
                new("Model.CountRecords", "Returns the number of records as a string."),
                new("Model.ElapsedMilliseconds", "Returns the query execution time in milliseconds as a string."), 
                new("Model.Records", "Returns a list of data records as IEnumerable<IDictionary<string, object>>."),
                new("Model.RecordColumns", "Returns the list of column names from the query."),
                new("Model.QueryString", "Returns a dictionary of URL parameters."),
                new("Model.FilterString", "Returns the query's URL parameters joined by '&'.")
        };
    }

    public record DataLinqFunctionDetail(
    string Name,
    string Syntax,
    List<(string ParamName, string ParamType, string Description, bool Required)> Parameters,
    string Example
    );

    [KernelFunction("get_datalinq_function_details")]
    [Description("Gets the details and usage information of a specific DataLinq function")]
    public DataLinqFunctionDetail GetDataLinqFunctionDetails([Description("The function name")] string functionName)
    {
        return functionName switch
        {
            "Table" => new(
                "@DLH.Table()",
                "@DLH.Table(records,columns,htmlAttributes,row0HtmlAttributes,row1HtmlAttributes,row2HtmlAttributes,cell0HtmlAttributes,cellHtmlAttributes,max)",
                new() 
                { 
                    ("records", "IEnumerable<IDictionary<string, object>>","Contains the data of the query of the view",true),
                    ("columns", "IEnumerable<string>","The columns to display in the table",false),
                    ("htmlAttributes", "object","CSS style object for the <table>",false),
                    ("row0HtmlAttributes", "object","CSS style object for the header row of the <table>",false),
                    ("row1HtmlAttributes", "object","CSS style object for every odd row of the <table>",false),
                    ("row2HtmlAttributes", "object","CSS style object for every even row of the <table>",false),
                    ("cell0HtmlAttributes", "object","CSS style object for every odd cell of the <table>",false),
                    ("cellHtmlAttributes", "object","CSS style object for every even cell of the <table>",false),
                    ("max", "int","The max amount of rows displayed in the table",false)
                },
                """
                @DLH.Table(
                    records: Model.Records,
                    columns: ["col1","col2","col3","col4","col5"],
                    htmlAttributes: new { style = "key:value;key:value" }, 
                    row0HtmlAttributes: new { style = "key:value;key:value" }, 
                    row1HtmlAttributes: new { style = "key:value;key:value" }, 
                    row2HtmlAttributes: new { style = "key:value;key:value" }, 
                    cell0HtmlAttributes: new { style = "key:value;key:value" }, 
                    cellHtmlAttributes: new { style = "key:value;key:value" }
                    max: 100
                )
                """
            ),
            "FilterView" => new(
                "DLH.FilterView()",
                "DLH.FilterView(label,filterParameters,htmlAttributes,isOpen)",
                new()
                {
                    ("label", "string","The lable of the filter control element",true),
                    ("filterParameters", "string[]","The column names to be filtered",true),
                    ("htmlAttributes", "object","CSS style object for the filter element",false),
                    ("isOpen", "bool","Setting if the filter element should be open or closed by default",false)
                },
                """
                @DLH.FilterView(
                    label: "Data Filter", 
                    filterParameters: ["col1","col2"],
                    htmlAttributes: new { style = "key:value;key:value" },
                    isOpen: true)
                """
            ),
            "SortView" => new(
                "DLH.SortView()",
                "DLH.SortView(label, orderFields, htmlAttributes, isOpen)",
                new()
                {
                    ("label", "string", "The label of the sort control element", true),
                    ("orderFields", "string[]", "The column names to sort by", true),
                    ("htmlAttributes", "object", "CSS style object for the sort element", false),
                    ("isOpen", "bool", "Whether the sort element is open or closed by default", false)
                },
                """
                @DLH.SortView(
                    label: "Sort Options", 
                    orderFields: ["col1", "col2"],
                    htmlAttributes: new { style = "key:value;key:value" },
                    isOpen: false)
                """
            ),
            "ExportView" => new(
                "DLH.ExportView()",
                "DLH.ExportView(label, htmlAttributes, columns)",
                new()
                {
                    ("label", "string", "The label of the export button", false),
                    ("htmlAttributes", "object", "CSS style object for the export button", false),
                    ("columns", "IEnumerable<string>", "The list of column names to include in the export (null = all columns)", false)
                },
                """
                @DLH.ExportView(
                    label: "Download CSV",
                    htmlAttributes: new { style = "key:value;key:value" },
                    columns: new[] { "col1", "col2", "col3" })
                """
            ),
            "JsFetchData" => new(
                "DLH.JsFetchData()",
                "DLH.JsFetchData(id, jsCallbackFuncName, filter, encodeUrl)",
                new()
                {
                    ("id", "string", "The unique identifier for the fetch request", true),
                    ("jsCallbackFuncName", "string", "The name of the JavaScript callback function to handle results", true),
                    ("filter", "string", "Optional filter expression applied to the data", false),
                    ("encodeUrl", "bool", "Whether the URL should be encoded", false)
                },
                """
                @DLH.JsFetchData(
                    id: "endpoint@query@view", 
                    jsCallbackFuncName: "onDataLoaded",
                    filter: "Status = 'Active'",
                    encodeUrl: true)
                """
            ),
            "RecordsToJs" => new(
                "DLH.RecordsToJs()",
                "DLH.RecordsToJs(records, jsCallbackFuncName)",
                new()
                {
                    ("records", "IDictionary<string, object>[]", "An array of record objects to be passed to JavaScript", true),
                    ("jsCallbackFuncName", "string", "The name of the JavaScript callback function to receive the records", true)
                },
                """
                @DLH.RecordsToJs(
                    records: Model.records,
                    jsCallbackFuncName: "onRecordsLoaded")
                """
            ), //FIX ORDERBY
            "GetRecordsAsync" => new(
                "DLH.GetRecordsAsync()",
                "await DLH.GetRecordsAsync(id, filter, orderby)",
                new()
                {
                    ("id", "string", "The unique identifier of the data source", true),
                    ("filter", "string", "Optional filter expression applied to the records", false),
                    ("orderby", "string", "Optional ordering expression for sorting the records", false)
                },
                """
                var records = await DLH.GetRecordsAsync(
                    id: "endpoint@query@view", 
                    filter: "Status = 'Active'", 
                    orderby: "CreatedDate DESC");
                """
            ),
            "IncludeView" => new(
                "DLH.IncludeView()",
                "DLH.IncludeView(id, encodeQueryString)",
                new()
                {
                    ("id", "string", "The unique identifier of the view to include", true),
                    ("encodeQueryString", "bool", "Whether to encode the query string in the view URL", false)
                },
                """
                @DLH.IncludeView(
                    id: "endpoint@query@view", 
                    encodeQueryString: true)
                """
            ),
            "IncludeClickView" => new(
                "DLH.IncludeClickView()",
                "DLH.IncludeClickView(id, text, encodeUrl)",
                new()
                {
                    ("id", "string", "The unique identifier of the view to include", true),
                    ("text", "string", "The display text for the clickable view link", true),
                    ("encodeUrl", "bool", "Whether the view URL should be encoded", false)
                },
                """
                @DLH.IncludeClickView(
                    id: "endpoint@query@view", 
                    text: "View Order Details", 
                    encodeUrl: true)
                """
            ),
            "RefreshViewClick" => new(
                "DLH.RefreshViewClick()",
                "DLH.RefreshViewClick(label, htmlAttributes)",
                new()
                {
                    ("label", "string", "The display text for the refresh button", false),
                    ("htmlAttributes", "object", "Optional HTML attributes for customizing the refresh element", false)
                },
                """
                @DLH.RefreshViewClick(
                    label: "Refresh Data", 
                    htmlAttributes: new { style="key:value;key:value" })
                """
            ),
            "RefreshViewTicker" => new(
                "DLH.RefreshViewTicker()",
                "DLH.RefreshViewTicker(label, seconds, htmlAttributes, isActive)",
                new()
                {
                    ("label", "string", "The display text shown before the countdown", false),
                    ("seconds", "int", "The interval in seconds before the view refreshes", false),
                    ("htmlAttributes", "object", "Optional HTML attributes for customizing the ticker element", false),
                    ("isActive", "bool", "Whether the refresh ticker is active on initialization", false)
                },
                """
                @DLH.RefreshViewTicker(
                    label: "Refreshing in", 
                    seconds: 30, 
                    htmlAttributes: new { style="key:value;key:value" }, 
                    isActive: true)
                """
            ), //FIX FILTERVALUE
            "UpdateFilterButton" => new(
                "DLH.UpdateFilterButton()",
                "DLH.UpdateFilterButton(filterName, filterValue, buttonText, filterId, htmlAttributes)",
                new()
                {
                    ("filterName", "string", "The name of the filter to update", true),
                    ("filterValue", "object", "The value to set for the specified filter", true),
                    ("buttonText", "string", "The display text of the filter button", false),
                    ("filterId", "string", "Optional identifier for the filter", false),
                    ("htmlAttributes", "object", "Optional HTML attributes for customizing the button element", false)
                },
                """
                @DLH.UpdateFilterButton(
                    filterName: "Status", 
                    filterValue: "Active", /
                    buttonText: "Apply Filter", 
                    filterId: "statusFilter", 
                    htmlAttributes: new { style="key:value;key:value" })
                """
            ),
            "BeginForm" => new(
                "DLH.BeginForm()",
                "DLH.BeginForm(id, htmlAttributes)",
                new()
                {
                    ("id", "string", "The unique identifier for the form", true),
                    ("htmlAttributes", "object", "Optional HTML attributes for customizing the form element", false)
                },
                """
                @DLH.BeginForm(
                    id: "endpoint@query@view", 
                    htmlAttributes: new { style="key:value;key:value" })
                """
            ),
            "EndForm" => new(
                "DLH.EndForm()",
                "DLH.EndForm(submitText, cancelText)",
                new()
                {
                    ("submitText", "string", "The text displayed on the form's submit button", false),
                    ("cancelText", "string", "The text displayed on the form's cancel button", false)
                },
                """
                @DLH.EndForm(
                    submitText: "Save", 
                    cancelText: "Cancel")
                """
            ),
            "OpenViewInDialog" => new(
                "DLH.OpenViewInDialog()",
                "DLH.OpenViewInDialog(id, parameter, htmlAttributes, buttonText, dialogAttributes)",
                new()
                {
                    ("id", "string", "The unique identifier of the view to open in the dialog", true),
                    ("parameter", "object", "Optional parameters to pass to the view", true),
                    ("htmlAttributes", "object", "Optional HTML attributes for the dialog trigger button", false),
                    ("buttonText", "string", "The text displayed on the button that opens the dialog", false),
                    ("dialogAttributes", "object", "Optional attributes to customize the dialog itself", false)
                },
                """
                @DLH.OpenViewInDialog(
                    id: "endpoint@query@view", 
                    parameter: new { key = value }, 
                    htmlAttributes: new { style="key:value;key:value" }, 
                    buttonText: "View Details", 
                    dialogAttributes: new { style="key:value;key:value" })
                """
            ),
            "ExecuteNonQuery" => new(
                "DLH.ExecuteNonQuery()",
                "DLH.ExecuteNonQuery(id, parameter, htmlAttributes, buttonText, dialogAttributes)",
                new()
                {
                    ("id", "string", "The unique identifier of the action or command to execute", true),
                    ("parameter", "object", "Optional parameters to pass to the command", true),
                    ("htmlAttributes", "object", "Optional HTML attributes for the trigger button", false),
                    ("buttonText", "string", "The text displayed on the button that executes the command", false),
                    ("dialogAttributes", "object", "Optional attributes to customize a dialog if the command opens one", false)
                },
                """
                @DLH.ExecuteNonQuery(
                    id: "endpoint@query@view", 
                    parameter: new { key = "value" }, 
                    htmlAttributes: new { style="key:value;key:value" }, 
                    buttonText: "Update", 
                    dialogAttributes: new { style="key:value;key:value" })
                """
            ),
            "ComboFor" => new(
                "DLH.ComboFor()",
                "DLH.ComboFor(record, name, htmlAttributes, source, defaultValue)",
                new()
                {
                    ("record", "object", "The data record containing the property for binding", true),
                    ("name", "string", "The property name of the record to bind the combo box to", true),
                    ("htmlAttributes", "object", "Optional HTML attributes to customize the combo box element", false),
                    ("source", "object", "Optional data source for the combo box options", false),
                    ("defaultValue", "object", "Optional default value to select in the combo box", false)
                },
                """
                @DLH.ComboFor(
                    record: null, 
                    name: "Country", 
                    htmlAttributes: new { style="key:value;key:value" }, 
                    source: new[] { "USA", "Canada", "Germany" }, 
                    defaultValue: "USA")
                """
            ),
            "RadioFor" => new(
                "DLH.RadioFor()",
                "DLH.RadioFor(record, name, htmlAttributes, source, defaultValue)",
                new()
                {
                    ("record", "object", "The data record containing the property for binding", true),
                    ("name", "string", "The property name of the record to bind the radio buttons to", true),
                    ("htmlAttributes", "object", "Optional HTML attributes to customize the radio button group", false),
                    ("source", "object", "Optional data source for the radio button options", false),
                    ("defaultValue", "object", "Optional default value to select in the radio button group", false)
                },
                """
                @DLH.RadioFor(
                    record: null, 
                    name: "Gender", 
                    htmlAttributes: new { style="key:value;key:value" }, 
                    source: new[] { "Male", "Female", "Other" }, 
                    defaultValue: "Male")
                """
            ),
            "TextFor" => new(
                "DLH.TextFor()",
                "DLH.TextFor(record, name, htmlAttributes, defaultValue, dataType)",
                new()
                {
                    ("record", "object", "The data record containing the property for binding", true),
                    ("name", "string", "The property name of the record to bind the text input to", true),
                    ("htmlAttributes", "object", "Optional HTML attributes to customize the text input element", false),
                    ("defaultValue", "object", "Optional default value for the text input", false),
                    ("dataType", "DataType", "The type of data to enforce for the text input (e.g., Text, Date, Email)", false)
                },
                """
                @DLH.TextFor(
                    record: null, 
                    name: "Email", 
                    htmlAttributes: new { style="key:value;key:value" }, 
                    defaultValue: "user@example.com", 
                    dataType: DataType.Text)
                """
            ),
            "CheckboxFor" => new(
                "DLH.CheckboxFor()",
                "DLH.CheckboxFor(record, name, htmlAttributes, defaultValue)",
                new()
                {
                    ("record", "object", "The data record containing the property for binding", true),
                    ("name", "string", "The property name of the record to bind the checkbox to", true),
                    ("htmlAttributes", "object", "Optional HTML attributes to customize the checkbox element", false),
                    ("defaultValue", "object", "Optional default value (checked/unchecked) for the checkbox", false)
                },
                """
                @DLH.CheckboxFor(
                    record: null, 
                    name: "IsActive", 
                    htmlAttributes: new { style="key:value;key:value" }, 
                    defaultValue: true)
                """
            ),
            "TextboxFor" => new(
                "DLH.TextboxFor()",
                "DLH.TextboxFor(record, name, htmlAttributes, defaultValue)",
                new()
                {
                    ("record", "object", "The data record containing the property for binding", true),
                    ("name", "string", "The property name of the record to bind the textbox to", true),
                    ("htmlAttributes", "object", "Optional HTML attributes to customize the textbox element", false),
                    ("defaultValue", "object", "Optional default value for the textbox", false)
                },
                """
                @DLH.TextboxFor(
                    record: null, 
                    name: "FirstName", 
                    htmlAttributes: new { style="key:value;key:value" }, 
                    defaultValue: "John")
                """
            ),
            "HiddenFor" => new(
                "DLH.HiddenFor()",
                "DLH.HiddenFor(record, name, htmlAttributes, defaultValue)",
                new()
                {
                    ("record", "object", "The data record containing the property for binding", true),
                    ("name", "string", "The property name of the record to bind the hidden input to", true),
                    ("htmlAttributes", "object", "Optional HTML attributes to customize the hidden input element", false),
                    ("defaultValue", "object", "Optional default value for the hidden input", false)
                },
                """
                @DLH.HiddenFor(
                    record: null, 
                    name: "CustomerId", 
                    htmlAttributes: new { style="key:value;key:value" }, 
                    defaultValue: 12345)
                """
            ),
            "LabelFor" => new(
                "DLH.LabelFor()",
                "DLH.LabelFor(label, name, htmlAttributes, newLine)",
                new()
                {
                    ("label", "string", "The text to display in the label", true),
                    ("name", "string", "The name of the input element this label is associated with", false),
                    ("htmlAttributes", "object", "Optional HTML attributes to customize the label element", false),
                    ("newLine", "bool", "Whether to render the label on a new line", false)
                },
                """
                @DLH.LabelFor(
                    label: "First Name", 
                    name: "FirstName", 
                    htmlAttributes: new { style="key:value;key:value" }, 
                    newLine: true)
                """
            ),
            "StatisticsCount" => new(
                "DLH.StatisticsCount()",
                "DLH.StatisticsCount(records, label, htmlAttributes)",
                new()
                {
                    ("records", "IDictionary<string, object>[]", "An array of record objects to count", true),
                    ("label", "string", "Optional label text displayed alongside the count", false),
                    ("htmlAttributes", "object", "Optional HTML attributes to customize the display element", false)
                },
                """
                @DLH.StatisticsCount(
                    records: Model.records,
                    label: "Total Records", 
                    htmlAttributes: null)
                """
            ), //FIX ORDERFIELD
            "StatisticsGroupBy" => new(
                "DLH.StatisticsGroupBy()",
                "DLH.StatisticsGroupBy(records, jsVariableName, field, orderField)",
                new()
                {
                    ("records", "IDictionary<string, object>[]", "An array of record objects to group", true),
                    ("jsVariableName", "string", "The name of the JavaScript variable to store the grouped data", true),
                    ("field", "string", "The field name to group the records by", true),
                    ("orderField", "OrderField", "The order in which to sort the grouped data", false)
                },
                """
                @DLH.StatisticsGroupBy(
                    records: Model.records,
                    jsVariableName: "groupedData", 
                    field: "Category", 
                    orderField: OrderField.Ascending)
                """
            ), //FIX orderfield and stattype
            "StatisticsGroupByDerived" => new(
                "DLH.StatisticsGroupByDerived()",
                "DLH.StatisticsGroupByDerived(records, jsVariableName, categoryField, valueField, statType, orderField)",
                new()
                {
                    ("records", "IDictionary<string, object>[]", "An array of record objects to process", true),
                    ("jsVariableName", "string", "The name of the JavaScript variable to store the computed results", true),
                    ("categoryField", "string", "The field name to group the records by", true),
                    ("valueField", "string", "The field name whose values will be aggregated", true),
                    ("statType", "StatType", "The type of aggregation to perform (e.g., Sum, Average)", false),
                    ("orderField", "OrderField", "The order in which to sort the grouped data", false)
                },
                """
                @DLH.StatisticsGroupByDerived(
                    records: Model.records,
                    jsVariableName: "derivedData", 
                    categoryField: "Category", 
                    valueField: "Value", 
                    statType: StatType.Sum, 
                    orderField: OrderField.Ascending)
                """
            ), //fix orderfield
            "StatisticsGroupByTime" => new(
                "DLH.StatisticsGroupByTime()",
                "DLH.StatisticsGroupByTime(records, jsVariableName, datetimeField, categoryField, secondsInterval, fillMissingDataValue, orderField)",
                new()
                {
                    ("records", "IDictionary<string, object>[]", "An array of record objects to process", true),
                    ("jsVariableName", "string", "The name of the JavaScript variable to store the results", true),
                    ("datetimeField", "string", "The field containing datetime values for grouping", true),
                    ("categoryField", "string", "Optional field name to further categorize the records", false),
                    ("secondsInterval", "int", "The interval in seconds for grouping the datetime values", false),
                    ("fillMissingDataValue", "object", "Optional value to use for missing data points", false),
                    ("orderField", "OrderField", "The order in which to sort the grouped data", false)
                },
                """
                @DLH.StatisticsGroupByTime(
                    records: Model.records,
                    jsVariableName: "timeGroupedData", 
                    datetimeField: "Timestamp", 
                    categoryField: "Category", 
                    secondsInterval: 300, 
                    fillMissingDataValue: 0, 
                    orderField: OrderField.Ascending)
                """
            ),
            "StatisticsTime" => new(
                "DLH.StatisticsTime()",
                "DLH.StatisticsTime(records, jsVariableName, datetimeField, valueField, categoryField)",
                new()
                {
                    ("records", "IDictionary<string, object>[]", "An array of record objects to process", true),
                    ("jsVariableName", "string", "The name of the JavaScript variable to store the results", true),
                    ("datetimeField", "string", "The field containing datetime values for analysis", true),
                    ("valueField", "string", "The field containing values to aggregate or analyze", true),
                    ("categoryField", "string", "Optional field name to categorize the records", false)
                },
                """
                @DLH.StatisticsTime(
                    records: Model.records,
                    jsVariableName: "timeStats", 
                    datetimeField: "Timestamp", 
                    valueField: "Value", 
                    categoryField: "Category")
                """
            ), //FIX CHART TYPE AND LOCALE
            "Chart" => new(
                "DLH.Chart()",
                "DLH.Chart(chartType, jsValueVariable, label, htmlAttributes, chartColorRGB, jsDatasetVariable, locale)",
                new()
                {
                    ("chartType", "ChartType", "The type of chart to render (e.g., Line, Bar, Pie)", true),
                    ("jsValueVariable", "string", "The name of the JavaScript variable containing the chart values", true),
                    ("label", "string", "Optional label text for the chart", false),
                    ("htmlAttributes", "object", "Optional HTML attributes for customizing the chart element", false),
                    ("chartColorRGB", "string[]", "Optional array of RGB color values for the chart datasets", false),
                    ("jsDatasetVariable", "string", "Optional JavaScript variable name for dataset configuration", false),
                    ("locale", "ChartLocale", "Optional locale setting for the chart (e.g., formatting, language)", false)
                },
                """
                @DLH.Chart(
                    chartType: ChartType.Bar, 
                    jsValueVariable: "chartValues", 
                    label: "Sales Report", 
                    htmlAttributes: new { style="key:value;key:value" }, 
                    chartColorRGB: new[] { "255,0,0", "0,255,0", "0,0,255" }, 
                    jsDatasetVariable: "chartDatasets", 
                    locale: ChartLocale.EN)
                """
            ), //FIX SOURCE
            "ExecuteScalar" => new(
                "DLH.ExecuteScalar()",
                "DLH.ExecuteScalar(htmlAttributes, source, htmlTag, defaultValue)",
                new()
                {
                    ("htmlAttributes", "object", "Optional HTML attributes to customize the element", false),
                    ("source", "object", "Optional data source or expression to evaluate", false),
                    ("htmlTag", "string", "The HTML tag to use for rendering the result", false),
                    ("defaultValue", "string", "Optional default value to display if the result is null", false)
                },
                """
                @DLH.ExecuteScalar(
                    htmlAttributes: new { style="key:value;key:value" }, 
                    source: someDataSource, 
                    htmlTag: "div", 
                    defaultValue: "N/A")
                """
            ),
            "ResponsiveSwitcher" => new(
                "DLH.ResponsiveSwitcher()",
                "DLH.ResponsiveSwitcher()",
                new()
                {
                },
                """
                @DLH.ResponsiveSwitcher()
                """
            ),
            "GetCurrentUsername" => new(
                "DLH.GetCurrentUsername()",
                "DLH.GetCurrentUsername()",
                new()
                {
                },
                """
                var username = DLH.GetCurrentUsername();
                """
            ),
            "HasRole" => new(
                "DLH.HasRole()",
                "DLH.HasRole(roleName)",
                new()
                {
                    ("roleName", "string", "The name of the role to check for the current user", true)
                },
                """
                bool hasRole = DLH.HasRole(
                    roleName: "Admin");
                """
            ),
            "GetRequestHeaderValue" => new(
                "DLH.GetRequestHeaderValue()",
                "DLH.GetRequestHeaderValue(header)",
                new()
                {
                    ("header", "string", "The name of the HTTP request header to retrieve the value from", true)
                },
                """
                string headerValue = DLH.GetRequestHeaderValue(
                    header: "User-Agent");
                """
            )
        };
    }

    [KernelFunction("get_datalinq_endpoint_query_view")]
    [Description("Gets the view code for a specific datalinq id consisting of endpoint,query,view devided by @")]
    [return: Description("The code for the DataLinq view")]
    public async Task<string> GetDataLinqEndPointQueryView(
    [Description("The endpoint id of the datalinq identifier")]
    string part1,
    [Description("The query id of the datalinq identifier")]
    string part2,
    [Description("The view id of the datalinq identifier")]
    string part3)
    {
        if (_client == null)
            throw new InvalidOperationException("DataLinq endpoint not configured");

        if (string.IsNullOrWhiteSpace(part1))
            throw new ArgumentException("Endpoint cannot be null or empty", nameof(part1));

        if (string.IsNullOrWhiteSpace(part2))
            throw new ArgumentException("Query cannot be null or empty", nameof(part2));

        if (string.IsNullOrWhiteSpace(part2))
            throw new ArgumentException("View cannot be null or empty", nameof(part3));

        try
        {
            var model = await _client.GetEndPointQueryView(part1, part2,part3);
            return model != null ? model.Code : string.Empty;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to retrieve DataLinq endpoint query view for '{part1}'@'{part2}'@'{part3}", ex);
        }
    }

    [KernelFunction("get_datalinq_endpoint_query")]
    [Description("Gets the query statement for a specific datalinq id consisting of endpoint,query devided by @")]
    [return: Description("The query definition or configuration for the DataLinq endpoint")]
    public async Task<string> GetDataLinqEndPointQuery(
   [Description("The endpoint id of the datalinq identifier")]
    string part1,
    [Description("The query id of the datalinq identifier")]
    string part2)
    {
        if (_client == null)
            throw new InvalidOperationException("DataLinq endpoint not configured");

        if (string.IsNullOrWhiteSpace(part1))
            throw new ArgumentException("Part1 cannot be null or empty", nameof(part1));

        if (string.IsNullOrWhiteSpace(part2))
            throw new ArgumentException("Part2 cannot be null or empty", nameof(part2));

        try
        {
            var model = await _client.GetEndPointQuery(part1, part2);
            return model != null ? model.Statement : string.Empty;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to retrieve DataLinq endpoint query for '{part1}' and '{part2}'", ex);
        }
    }

    [KernelFunction("get_datalinq_endpoint")]
    [Description("Gets the endpoint configuration for a specific datalinq id consisting of endpoint")]
    [return: Description("The endpoint definition or configuration for the DataLinq identifier")]
    public async Task<string> GetDataLinqEndPoint(
        [Description("The datalinq endpoint identifier")]
    string part1)
    {
        if (_client == null)
            throw new InvalidOperationException("DataLinq endpoint not configured");

        if (string.IsNullOrWhiteSpace(part1))
            throw new ArgumentException("Part1 cannot be null or empty", nameof(part1));

        try
        {
            var model = await _client.GetEndPoint(part1);
            return model != null ? JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true }) : string.Empty;

        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to retrieve DataLinq endpoint for '{part1}'", ex);
        }
    }

    #region Helpers
    private string formatDataLinqName(string input)
    {
        return input.ToLower().Replace('_', '-').Replace(" ","");
    }
    #endregion

}
