using Newtonsoft.Json;
using System;
using System.ComponentModel;

namespace E.DataLinq.Core.Models;

public class DataLinqEndPointQueryView
{
    [JsonProperty("id")]
    [DisplayName("View Id")]
    [Description("The unique view id (readonly)")]
    public string ViewId { get; set; }

    [JsonProperty("name")]
    [Description("a meaningful name for the query")]
    public string Name { get; set; }

    [JsonProperty("description")]
    [Description("Here you can describe the intended use for the query")]
    public string Description { get; set; }

    [JsonProperty("code")]
    public string Code { get; set; }

    [JsonProperty("created")]
    public DateTime Created { get; set; }

    [JsonProperty("changed")]
    public DateTime Changed { get; set; }

    [JsonIgnore]
    public string ErrorMessage { get; set; }

    [JsonIgnore]
    public string EndPointId { get; set; }

    [JsonIgnore]
    public string QueryId { get; set; }

    [JsonIgnore]
    public bool ShowCode { get; set; }

    [JsonProperty(PropertyName = "test_parameters")]
    [DisplayName("Test Url Parameters")]
    [Description("Url parameters can be specified here, which are automatically appended during a test/debug call from the IDE")]
    public string TestParameters { get; set; }

    [JsonProperty("included_js_libs")]
    [Description("Select JavaScript libraries here that should be loaded when the report is accessed. This option is only considered if the view is the main/startpage of the report. For subpages, this option is irrelevant. If a subpage uses JavaScript libraries, they must be included on the startpage.")]
    public string IncludedJsLibraries { get; set; }

    [JsonProperty(PropertyName = "allow_code_reflection")]
    [DisplayName("Allow Code Reflection")]
    [Description("Use this option only if you develpe a guide for datalinq, where you also want to show code and query statements in the datalinq app. !! Do never use this options for production code!!")]
    public bool AllowCodeReflection { get; set; }
}
