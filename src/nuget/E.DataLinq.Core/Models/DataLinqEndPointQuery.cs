using E.DataLinq.Core.Models.Abstraction;
using E.DataLinq.Core.Reflection;
using Newtonsoft.Json;
using System;
using System.ComponentModel;

namespace E.DataLinq.Core.Models;

public class DataLinqEndPointQuery : IDataLinqAuthProperties
{
    [JsonProperty("id")]
    [DisplayName("#queryId")]
    [Description("#description_queryId")]
    public string QueryId { get; set; }

    [JsonProperty("name")]
    [Description("#description_queryName")]
    public string Name { get; set; }

    [JsonProperty("description")]
    [DisplayName("#queryDescription")]
    [Description("#description_queryDescription")]
    public string Description { get; set; }

    [SecureString]
    [JsonProperty("statement")]
    public string Statement { get; set; }

    [JsonProperty("access")]
    [DisplayName("#access")]
    [Description("#description_queryAccess")]
    public string[] Access { get; set; }

    [JsonProperty("access-tokens", NullValueHandling = NullValueHandling.Ignore)]
    [DisplayName("#accessTokens")]
    [Description("#description_queryAccessTokens")]
    public string[] AccessTokens { get; set; }

    [DisplayName("#created")]
    [JsonProperty("created")]
    public DateTime Created { get; set; }

    [JsonIgnore]
    public string ErrorMessage { get; set; }

    [JsonIgnore]
    public string EndPointId { get; set; }

    [JsonIgnore]
    public bool ShowCode { get; set; }

    [JsonProperty(PropertyName = "domains", NullValueHandling = NullValueHandling.Ignore)]
    public Domain[] Domains { get; set; }

    [JsonProperty(PropertyName = "test_parameters")]
    [DisplayName("#queryTestParameters")]
    [Description("#description_queryTestParameters")]
    public string TestParameters { get; set; }

    [JsonIgnore]
    public string Route => $"{EndPointId}@{QueryId}";

    #region Classes

    public class Domain
    {
        [JsonProperty("dest_field")]
        public string DestinationField { get; set; }
        [JsonProperty("query_id")]
        public string QueryId { get; set; }
        [JsonProperty("value_field")]
        public string ValueField { get; set; }
        [JsonProperty("name_field")]
        public string NameField { get; set; }
    }

    #endregion
}
