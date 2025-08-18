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
    Cypher = 8
    //Plugin = 4      //  Reserved: Legacy
}

public class DataLinqEndPoint : IDataLinqAuthProperties
{
    [JsonProperty("id")]
    [DisplayName("EndPoint Id")]
    [Description("The unique endpoint id (readonly)")]
    public string Id { get; set; }

    [JsonProperty("name")]
    [Description("a meaningful name for the endpoint")]
    public string Name { get; set; }

    [JsonProperty("description")]
    [Description("Here you can describe the intended use for the endpoint")]
    public string Description { get; set; }

    [JsonProperty("access")]
    [Description("Add or remove users and roles that can access the endpoint. Use * (Asterisk) as username to make this endpoint accessable for every user.")]
    public string[] Access { get; set; }

    [JsonProperty("access-tokens", NullValueHandling = NullValueHandling.Ignore)]
    [DisplayName("Access Tokens")]
    [Description("Here you can specify whether the endpoint can be accessed via tokens. Both or no token must be set")]
    public string[] AccessTokens { get; set; }

    [JsonProperty("created")]
    public DateTime Created { get; set; }

    [JsonProperty("subscriber-name")]
    public string Subscriber { get; set; }

    [JsonProperty("subscriber-id")]
    public string SubscriberId { get; set; }

    [JsonProperty("typevalue")]
    [DisplayName("EndPoint Connection Type")]
    [Description("Here you can specify whether the endpoint points to a database or a datalinq engine")]
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
    [DisplayName("Connection String (Default/Production)")]
    [Description("The Connection Sting to a database or an url to an service. For PlainText this this can be empty. This connection string is used, if the DataLinq Environment is 'default' or 'production' or DevTest connection isn't set.")]
    public string ConnectionString { get; set; }

    [SecureString]
    [JsonProperty("connectionstring_devtest")]
    [DisplayName("Connection String (Development/Test)")]
    [Description("The Connection Sting to a database or an url to an service. For PlainText this this can be empty. This connection string is used, if the Datadalinq Environment is 'develpment' or 'test'")]
    public string ConnectionString_DevTest { get; set; }

    [JsonIgnore]
    public string ErrorMessage { get; set; }

    [JsonProperty("resourceGuid", NullValueHandling = NullValueHandling.Ignore)]
    public string ResourceGuid { get; set; }

    [JsonIgnore]
    public string Route => Id;
}
