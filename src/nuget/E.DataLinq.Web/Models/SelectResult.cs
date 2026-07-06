using E.DataLinq.Core;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Razor;
using E.DataLinq.Web.Services;
using E.DataLinq.Web.Services.Abstraction;
using E.DataLinq.Web.Services.TokenCache;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Models;

public class SelectResult
{
    private readonly HttpContext _httpContext;
    private readonly IRazorCompileEngineService _razorEngine;
    private readonly DataLinqService _datalinq;

    private SelectResult(HttpContext httpContext,
                         IRazorCompileEngineService razorEngine,
                         DataLinqService datalinq,
                         HttpRequest request,
                         DateTime startTime,
                         object[] records,
                         IDataLinqUser ui)
    {
        _httpContext = httpContext;
        _razorEngine = razorEngine;
        _datalinq = datalinq;

        ElapsedMillisconds = Convert.ToInt32((DateTime.Now - startTime).TotalMilliseconds);
        Success = true;
        CountRecords = records?.Length ?? 0;
        Result = records;

        QueryString = request.Query.ToCollection();

        Environment = new SelectEnvironment
        {
            CurrentUser = ui?.Username ?? String.Empty
        };

        UserInformation = ui;
    }

    public static async Task<SelectResult> CreateAsync(
        HttpContext httpContext,
        IRazorCompileEngineService razorEngine,
        DataLinqService datalinq,
        IDataLinqCacheTokenService cacheTokenService,
        HttpRequest request,
        DateTime startTime,
        object[] records,
        IDataLinqUser ui)
    {
        var result = new SelectResult(
            httpContext,
            razorEngine,
            datalinq,
            request,
            startTime,
            records,
            ui);

        var paramName = cacheTokenService.UrlParamterName;
        var paramValue = result.QueryString[paramName];

        if (!string.IsNullOrEmpty(paramValue))
        {
            var tokenResolveResponse = await cacheTokenService.ResolveTokenAsync(paramValue, false);
            if (tokenResolveResponse.Success)
            {
                result.QueryString = tokenResolveResponse.Payload.ParseCacheTokenPayload();
            }
        }

        result.FilterString = result.QueryString
            .Clone(new[] { "_orderby", "_f", "_id", "hmac", "hmac_pubk", "hmac_ts", "hmac_data", "hmac_hash", "__gdi" })
            .ToFilterString();

        return result;
    }

    [JsonProperty(PropertyName = "success")]
    public bool Success { get; set; }

    [JsonProperty(PropertyName = "count")]
    public int CountRecords { get; set; }

    [JsonProperty("elapsed_ms")]
    public int ElapsedMillisconds { get; set; }

    [JsonProperty(PropertyName = "data")]
    public object[] Result { get; set; }

    [JsonIgnore]
    public IDictionary<string, object>[] Records
    {
        get
        {
            var records = new List<IDictionary<string, object>>();

            foreach (var record in this.Result)
            {
                if (record is ExpandoObject expando)
                {
                    records.Add(expando);
                }
                else
                {
                    var dict = ConvertToExpando(record);
                    if (dict != null)
                        records.Add(dict);
                }
            }

            return records.ToArray();
        }
    }

    private static IDictionary<string, object> ConvertToExpando(object obj)
    {
        if (obj == null) return null;

        if (obj is IDictionary<string, object> dictionary)
            return dictionary;

        var expando = new ExpandoObject() as IDictionary<string, object>;

        var props = obj.GetType().GetProperties();
        foreach (var prop in props)
        {
            if (prop.GetIndexParameters().Length == 0)
            {
                var val = prop.GetValue(obj);
                expando[prop.Name] = val ?? "";
            }
        }

        return expando;
    }

    public IEnumerable<string> RecordColumns(bool deepSearch = false)
    {
        if (this.Records == null || this.Records.Count() == 0)
        {
            return new string[0];
        }

        List<string> columns = new List<string>();

        foreach (var record in this.Records)
        {
            if (record != null)
            {
                foreach (var key in record.Keys)
                {
                    if (!columns.Contains(key))
                    {
                        columns.Add(key);
                    }
                }

                if (deepSearch == false)
                {
                    break;
                }
            }
        }

        return columns.ToArray();
    }

    public NameValueCollection QueryString
    {
        get; set;
    }

    [JsonIgnore]
    public string FilterString
    {
        get; set;
    }

    public DataLinqHelper CreateDataLinqHelper()
    {
        return new DataLinqHelper(_httpContext, _datalinq, _razorEngine, this.UserInformation);
    }

    public DataLinqPdfHelper CreateDataLinqPdfHelper()
    {
        return new DataLinqPdfHelper(_razorEngine);
    }

    [JsonProperty("environment")]
    public SelectEnvironment Environment
    {
        get; set;
    }

    [JsonIgnore]
    private IDataLinqUser UserInformation { get; set; }

    #region Classes

    public class SelectEnvironment
    {
        [JsonProperty(PropertyName = "current_user")]
        public string CurrentUser { get; set; }

        [JsonProperty(PropertyName = "current_user_roletype")]
        public string CurrentUserRoleType
        {
            get
            {
                int index = this.CurrentUser.IndexOf(':');
                return (index > 0) ? this.CurrentUser.Substring(0, index) : "";
            }
        }

        [JsonProperty(PropertyName = "current_user_domain")]
        public string CurrentUserDomain
        {
            get
            {
                int indexRole = this.CurrentUser.LastIndexOf(':');
                string helper = (indexRole > 0) ? this.CurrentUser.Substring(indexRole + 1) : this.CurrentUser;
                int indexDomain = helper.IndexOf(@"\");
                return (indexDomain > 0) ? helper.Substring(0, indexDomain) : "";
            }
        }

        [JsonProperty(PropertyName = "current_user_name")]
        public string CurrentUserName
        {
            get
            {
                int indexRole = this.CurrentUser.LastIndexOf(':');
                string helper = (indexRole > 0) ? this.CurrentUser.Substring(indexRole + 1) : this.CurrentUser;
                int indexDomain = helper.LastIndexOf(@"\");
                return (indexDomain > 0) ? helper.Substring(indexDomain + 1) : helper;
            }
        }

        [JsonProperty(PropertyName = "current_user_logon_name")]
        public string CurrentUserLogonName
        {
            get
            {
                if (String.IsNullOrEmpty(this.CurrentUserDomain))
                {
                    return this.CurrentUserName;
                }
                else
                {
                    return this.CurrentUserDomain + @"\" + this.CurrentUserName;
                }
            }
        }
    }

    #endregion
}
