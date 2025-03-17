namespace E.DataLinq.Code.Models.DataLinqCode;

public class IndexModel
{
    public string InstanceName { get; set; }
    public string CurrentUsername { get; set; }
    public string CurrentUrl { get; set; }
    public string DataLinqEngineUrl { get; set; }
    public string AccessToken { get; set; }

    public bool UseAppPrefixFilters { get; set; }

    public bool AllowCreateAndDeleteEndpoints { get; set; }
    public bool ALlowCreateAndDeleteQueries { get; set; }
    public bool AllowCreateAndDeleteViews { get; set; }
}
