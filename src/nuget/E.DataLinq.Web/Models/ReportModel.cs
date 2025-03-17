namespace E.DataLinq.Web.Models;

public class ReportModel
{
    public string Id { get; set; }
    public string QueryString { get; set; }
    public string OrderBy { get; set; }
    public string ClientSideAuthObjectString { get; set; }
    public string Html { get; set; }

    public string AuthIntialText { get; set; }

    public string EndpointId { get { return this.Id.Split('@')[0]; } }

    public string[] IncludedJsLibraries { get; set; }
}
