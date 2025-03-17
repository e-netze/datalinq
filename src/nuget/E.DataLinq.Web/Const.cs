using E.DataLinq.Core.Models;

namespace E.DataLinq.Web;

internal static class JsLibNames
{
    // Never Change this Constants!!!
    // they are stored in report view models!

    public const string Legacy_ChartJs = JsLibrary.LegacyDefaultNames;
    public const string Legacy_ChartJs_Plugin_DataLabels = "legacy_chartjs_plugin_datalabels";
    public const string Legacy_ChartJs_Plugin_ColorSchemes = "legacy_chartjs_plugin_colorschemes";

    public const string ChartJs_3x = "chartjs_3x";
    public const string ChartJs_3x_Plugin_DataLabels = "chartjs_3x_plugin_datalabels";

    public const string D3_7x = "d3_v7x";

    public const string RazorEngineClassic = "RazorEngine";
    public const string RazorEngineCore = "RazorEngineCore";
    public const string RazorEngineDataLinqLanguageEngine = "DataLinqLanguageEngine";
}

public static class RazorEngineIds
{
    public const string LegacyEngine = "RazorEngine";
    public const string DataLinqLanguageEngineRazor = "DataLinqLanguageEngineRazor";
}