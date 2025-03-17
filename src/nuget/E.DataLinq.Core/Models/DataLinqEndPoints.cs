using Newtonsoft.Json;

namespace E.DataLinq.Core.Models;

public class DataLinqEndPoints
{
    [JsonProperty("endpoints")]
    public DataLinqEndPoint[] EndPoints { get; set; }
}
