using Newtonsoft.Json;

namespace E.DataLinq.Core.Models;

public class SuccessCreatedModel : SuccessModel
{
    public SuccessCreatedModel(bool result)
        : base(result) { }

    [JsonProperty("endPoint", NullValueHandling = NullValueHandling.Ignore)]
    public string EndPointId { get; set; }

    [JsonProperty("query", NullValueHandling = NullValueHandling.Ignore)]
    public string QueryId { get; set; }

    [JsonProperty("view", NullValueHandling = NullValueHandling.Ignore)]
    public string ViewId { get; set; }
}
