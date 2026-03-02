using E.DataLinq.Core.Models.Abstraction;
using E.DataLinq.Core.Reflection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace E.DataLinq.Core.Models;

public enum DefaultEndPointTypes  // NEVER CHANGE VALUES !!!
{
    Unknown = 0,
    Database = 1,
    //WebGISApi = 2,  //  Reserved: Legacy
    DataLinq = 3,
    PlainText = 5,
    TextFile = 6,
    JsonApi = 7,
    Cypher = 8
}

public class DataLinqEndPoint : IDataLinqAuthProperties
{
    [JsonProperty("id")]
    [DisplayName("#endPointId")]
    [Description("#description_endPointId")]
    public string Id { get; set; }

    [JsonProperty("name")]
    [Description("#description_endPointName")]
    public string Name { get; set; }

    [JsonProperty("description")]
    [DisplayName("#endPointDescription")]
    [Description("#description_endPointDescription")]
    public string Description { get; set; }

    [JsonProperty("access")]
    [Description("#description_endPointAccess")]
    public string[] Access { get; set; }

    [JsonProperty("access-tokens", NullValueHandling = NullValueHandling.Ignore)]
    [DisplayName("#accessTokens")]
    [Description("#description_accessTokens")]
    public string[] AccessTokens { get; set; }

    [JsonProperty("created")]
    public DateTime Created { get; set; }

    [JsonProperty("subscriber-name")]
    public string Subscriber { get; set; }

    [JsonProperty("subscriber-id")]
    public string SubscriberId { get; set; }

    [JsonProperty("typevalue")]
    [DisplayName("#endPointConnectionTypes")]
    [Description("#description_endPointConnectionTypes")]
    public int TypeValue
    {
        get;
        set;
    }

    [JsonProperty("plugin")]
    public string Plugin { get; set; }

    [JsonIgnore]
    public IEnumerable<string> Plugins { get; set; }

    [SecureString]
    [JsonProperty("connectionstring")]
    [DisplayName("#conStringProd")]
    [Description("#description_conStringProd")]
    public string ConnectionString { get; set; }

    [SecureString]
    [JsonProperty("connectionstring_devtest")]
    [DisplayName("#conStringProd")]
    [Description("#description_conStringProd")]
    public string ConnectionString_DevTest { get; set; }

    [JsonIgnore]
    public string ErrorMessage { get; set; }

    [JsonProperty("resourceGuid", NullValueHandling = NullValueHandling.Ignore)]
    public string ResourceGuid { get; set; }

    [JsonIgnore]
    public string Route => Id;
}
