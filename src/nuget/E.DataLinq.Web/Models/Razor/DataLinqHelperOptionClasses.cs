using System;
using System.Collections.Generic;
using System.Text;

namespace E.DataLinq.Web.Models.Razor;

public class PageNumberOptions
{
    public bool UsePageNumbers { get; set; } = false;
    public int Position { get; set; } = 0;
    public int Type { get; set; } = 0;
    public int SkipPages { get; set; } = 0;

    public static implicit operator PageNumberOptions(Dictionary<string, object> d)
    {
        d ??= new Dictionary<string, object>();
        return new PageNumberOptions
        {
            UsePageNumbers = d.ContainsKey("UsePageNumbers") && (bool)d["UsePageNumbers"],
            Position = d.ContainsKey("Position") ? Convert.ToInt32(d["Position"]) : 0,
            Type = d.ContainsKey("Type") ? Convert.ToInt32(d["Type"]) : 0,
            SkipPages = d.ContainsKey("SkipPages") ? Convert.ToInt32(d["SkipPages"]) : 0,
        };
    }
}

public class PageTemplateOptions
{
    public bool UsePageTemplate { get; set; } = false;
    public string TemplateId { get; set; } = "";

    public static implicit operator PageTemplateOptions(Dictionary<string, object> d)
    {
        d ??= new Dictionary<string, object>();
        return new PageTemplateOptions
        {
            UsePageTemplate = d.ContainsKey("UsePageTemplate") && (bool)d["UsePageTemplate"],
            TemplateId = d.ContainsKey("TemplateId") ? d["TemplateId"].ToString() : "",
        };
    }
}

public class DynamicTableOptions
{
    public bool Dynamic { get; set; } = false;
    public bool DynamicUseTemplate { get; set; } = false;
    public double DefaultMarginTop { get; set; } = 10;
    public double DefaultMarginBottom { get; set; } = 10;
    public bool RepeatHeader { get; set; } = true;

    public static implicit operator DynamicTableOptions(Dictionary<string, object> d)
    {
        d ??= new Dictionary<string, object>();
        return new DynamicTableOptions
        {
            Dynamic = d.ContainsKey("Dynamic") && (bool)d["Dynamic"],
            DynamicUseTemplate = d.ContainsKey("DynamicUseTemplate") && (bool)d["DynamicUseTemplate"],
            DefaultMarginTop = d.ContainsKey("DefaultMarginTop") ? Convert.ToDouble(d["DefaultMarginTop"]) : 10,
            DefaultMarginBottom = d.ContainsKey("DefaultMarginBottom") ? Convert.ToDouble(d["DefaultMarginBottom"]) : 10,
            RepeatHeader = !d.ContainsKey("RepeatHeader") || (bool)d["RepeatHeader"],
        };
    }
}
