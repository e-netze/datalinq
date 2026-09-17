using System;
using System.Collections.Generic;
using System.Text;
using static QRCoder.PayloadGenerator.Girocode;

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

public class PageDateTimeOptions
{
    public bool UsePageDateTime { get; set; } = false;
    public int Position { get; set; } = 0;
    public string Format { get; set; } = "HH:mm dd.MM.yyyy";
    public int SkipPages { get; set; } = 0;

    public static implicit operator PageDateTimeOptions(Dictionary<string, object> d)
    {
        d ??= new Dictionary<string, object>();
        return new PageDateTimeOptions
        {
            UsePageDateTime = d.ContainsKey("UsePageDateTime") && (bool)d["UsePageDateTime"],
            Position = d.ContainsKey("Position") ? Convert.ToInt32(d["Position"]) : 0,
            Format = d.ContainsKey("Format") ? d["Format"].ToString() : "HH:mm dd.MM.yyyy",
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

public class MailQrCodeDetails
{
    public string reciever { get; set; } = "max.musermann@mail.at";
    public string subject { get; set; } = "Mail Betreff";
    public string body { get; set; } = "Mail Body";
    public QrCodeMailEncoding mailEncoding { get; set; } = QrCodeMailEncoding.MAILTO;
}

public class GeoLocationQrCodeDetails
{
    public string latitude { get; set; } = "47.05566";
    public string longitude { get; set; } = "15.4365";
    public QrCodeGeolocationEncoding geolocationEncoding { get; set; } = QrCodeGeolocationEncoding.GEO;
}

public class GiroQrCodeDetails
{
    public string iban { get; set; } = "47.05566";
    public string bic { get; set; } = "15.4365";
    public string name { get; set; } = "Max Mustermann";
    public decimal amount { get; set; } = 0;
    public string purpose { get; set; } = "Rechnung 1234";
    public QrCodeGiroTypeOfRemittance purposeType { get; set; } = QrCodeGiroTypeOfRemittance.Structured;
    public string purposeOfCreditTransfer { get; set; } = "Rechnung 1234";
    public string messageToGirocodeRecipient { get; set; } = "Vielen Dank für Ihre Zahlung!";
}

public enum QrCodeMailEncoding
{
    MAILTO,
    MATMSG,
    SMTP
}

public enum QrCodeGeolocationEncoding
{
    GEO,
    GoogleMaps
}

public enum QrCodeGiroTypeOfRemittance
{
    Structured,
    Unstructured
}
