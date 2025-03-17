using E.DataLinq.Core;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Razor;
using E.DataLinq.Web.Services;
using E.DataLinq.Web.Services.Abstraction;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Dynamic;
using System.Linq;

namespace E.DataLinq.Web.Models;

public class SelectResult
{
    private readonly HttpContext _httpContext;
    private readonly IRazorCompileEngineService _razorEngine;
    private readonly DataLinqService _datalinq;

    public SelectResult(HttpContext httpContext,
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

        this.ElapsedMillisconds = Convert.ToInt32((DateTime.Now - startTime).TotalMilliseconds);
        this.Success = true;
        this.CountRecords = records != null ? records.Length : 0;
        this.Result = records;

        this.QueryString = request.Query.ToCollection();
        // nur die Filterparameter aus der URL auslesen
        this.FilterString = this.QueryString
            .Clone(new string[] { "_orderby", "_f", "_id", "hmac", "hmac_pubk", "hmac_ts", "hmac_data", "hmac_hash", "__gdi" })
            .ToFilterString();

        this.Environment = new SelectEnvironment()
        {
            CurrentUser = ui?.Username ?? String.Empty
        };

        this.UserInformation = ui;
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
            List<ExpandoObject> records = new List<ExpandoObject>();
            foreach (var record in this.Result)
            {
                if (record is ExpandoObject)
                {
                    records.Add((ExpandoObject)record);
                }
            }
            return records.ToArray();
        }
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
