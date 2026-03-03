using Newtonsoft.Json;
using System;
using System.ComponentModel;

namespace E.DataLinq.Core.Models;

public class DataLinqEndPointQueryView
{
    [JsonProperty("id")]
    [DisplayName("#viewId")]
    [Description("#description_viewId")]
    public string ViewId { get; set; }

    [JsonProperty("name")]
    [Description("#description_viewName")]
    public string Name { get; set; }

    [JsonProperty("description")]
    [DisplayName("#viewDescription")]
    [Description("#description_viewDescription")]
    public string Description { get; set; }

    [JsonProperty("code")]
    public string Code { get; set; }

    [JsonProperty("created")]
    [DisplayName("#created")]
    public DateTime Created { get; set; }

    [JsonProperty("changed")]
    [DisplayName("#changed")]
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
    [DisplayName("#queryTestParameters")]
    [Description("#viewTestParameters")]
    public string TestParameters { get; set; }

    [JsonProperty("included_js_libs")]
    [Description("#description_jslibs")]
    public string IncludedJsLibraries { get; set; }

    [JsonProperty("pdf_report_mode")]
    [Description("#description_pdfReportModeView")]
    [DisplayName("#pdfReportModeView")]
    public bool PDFReportMode { get; set; }

    [JsonProperty("pdf_report_editing")]
    [Description("#description_pdfEditingMode")]
    [DisplayName("#pdfEditingMode")]
    public bool PDFReportEditing { get; set; }
}
