using E.DataLinq.Core.Models.Abstraction;
using E.DataLinq.Core.Reflection;
using Newtonsoft.Json;
using System;
using System.ComponentModel;

namespace E.DataLinq.Core.Models;

public class DataLinqEndPointQuery : IDataLinqAuthProperties
{
    [JsonProperty("id")]
    [DisplayName("Query Id")]
    [Description("The unique query id (readonly)")]
    public string QueryId { get; set; }

    [JsonProperty("name")]
    [Description("a meaningful name for the query")]
    public string Name { get; set; }

    [JsonProperty("description")]
    [Description("Here you can describe the intended use for the query")]
    public string Description { get; set; }

    [SecureString]
    [JsonProperty("statement")]
    public string Statement { get; set; }

    [JsonProperty("access")]
    [Description("Add or remove users and roles that can access the query. Use * (Asterisk) as username to make this query accessable for every user.")]
    public string[] Access { get; set; }

    [JsonProperty("access-tokens", NullValueHandling = NullValueHandling.Ignore)]
    [DisplayName("Access Tokens")]
    [Description("Here you can specify whether the query can be accessed via tokens. Both or no token must be set")]
    public string[] AccessTokens { get; set; }

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
    [DisplayName("Test Url Parameters")]
    [Description("Url parameters can be specified here, which are automatically appended during a test/debug call from the IDE")]
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
