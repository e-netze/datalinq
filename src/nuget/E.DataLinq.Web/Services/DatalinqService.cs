using E.DataLinq.Core;
using E.DataLinq.Core.Engines.Abstraction;
using E.DataLinq.Core.Extensions;
using E.DataLinq.Core.Models;
using E.DataLinq.Core.Models.Authentication;
using E.DataLinq.Core.Services.Abstraction;
using E.DataLinq.Core.Services.Persistance.Abstraction;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Models;
using E.DataLinq.Web.Services.Abstraction;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Web;

namespace E.DataLinq.Web.Services;

public class DataLinqService
{
    private readonly ILogger<DataLinqService> _logger;
    private readonly DataLinqOptions _options;
    private readonly IPersistanceProviderService _persistanceProvider;
    private readonly DataLinqCompilerService _datalinqCompiler;
    private readonly AccessControlService _accessControl;
    private readonly IEnumerable<IDataLinqSelectEngine> _dataLinqQueryEngines;
    private readonly IEnumerable<ISelectResultProvider> _selectResultProviders;
    private readonly DataLinqEndpointTypeService _endpointTypes;
    private readonly IWebHostEnvironment _environment;
    private readonly IHostAuthenticationService _hostAuthentication;
    private readonly IDataLinqCodeIdentityService _dataLinqCodeIdentity;
    private readonly IDataLinqAccessProviderService _dataLinqAccessProvider;

    public DataLinqService(ILogger<DataLinqService> logger,
                           IOptionsMonitor<DataLinqOptions> optionsMonitor,
                           IPersistanceProviderService persistanceProvider,
                           DataLinqCompilerService dataLinqCompiler,
                           AccessControlService accessControl,
                           IEnumerable<IDataLinqSelectEngine> dataLinqQueryEngines,
                           IEnumerable<ISelectResultProvider> selectResultProviders,
                           DataLinqEndpointTypeService endpointTypes,
                           IWebHostEnvironment environment,
                           IDataLinqAccessProviderService dataLinqAccessProvider,
                           IHostAuthenticationService hostAuthenication = null,
                           IDataLinqCodeIdentityService dataLinqCodeIdentity = null)
    {
        _logger = logger;
        _options = optionsMonitor.CurrentValue;
        _persistanceProvider = persistanceProvider;
        _datalinqCompiler = dataLinqCompiler;
        _accessControl = accessControl;
        _dataLinqQueryEngines = dataLinqQueryEngines;
        _selectResultProviders = selectResultProviders;
        _endpointTypes = endpointTypes;
        _environment = environment;
        _hostAuthentication = hostAuthenication;
        _dataLinqCodeIdentity = dataLinqCodeIdentity;
        _dataLinqAccessProvider = dataLinqAccessProvider;
    }

    async public Task<(object result, string contentType, bool succeeded)> QueryAsync(HttpContext httpContext,
                                                                                      string routeString,
                                                                                      NameValueCollection arguments,
                                                                                      bool isDomainQuery = false)
    {
        var dataLinqRoute = new DataLinqRoute(routeString, httpContext);
        bool succeeded = true;
        string contentType = String.Empty;

        try
        {
            var startTime = DateTime.Now;

            if (!String.IsNullOrWhiteSpace(routeString))
            {
                var endPoint = await _persistanceProvider.GetEndPoint(dataLinqRoute.EndpointId);
                if (endPoint == null)
                {
                    throw new ArgumentException($"Select -> Endpoint not found: {dataLinqRoute.EndpointId}");
                }

                if (!await IsAllowed(httpContext, dataLinqRoute, endPoint))
                {
                    throw new AuthenticationException($"Select -> Endpoint ({dataLinqRoute.EndpointId}): {CurrentUsername(httpContext)} not authorized");
                }

                var datalinqQueryEngine = _dataLinqQueryEngines.Where(e => e.EndpointType == endPoint.TypeValue).FirstOrDefault();
                if (datalinqQueryEngine == null)
                {
                    string endPointTypeName = _endpointTypes.TypeDictionary.ContainsKey(endPoint.TypeValue) ?
                        $"{endPoint.TypeValue}: {_endpointTypes.TypeDictionary[endPoint.TypeValue]}" :
                        endPoint.TypeValue.ToString();

                    throw new Exception($"Endpoint type ({endPointTypeName}) not supported");
                }

                if (!dataLinqRoute.HasQuery)
                {
                    #region Test Connection

                    if (_dataLinqCodeIdentity?.CurrentIdentity()?.HasEndPointRoleParameter(endPoint.Id) == true)
                    {
                        try
                        {
                            if (await datalinqQueryEngine.TestConnection(endPoint))
                            {
                                return (new { environment = _options.EnvironmentType.ToString(), succeeded = true, }, "application/json", true);
                            }
                        }
                        catch (Exception ex)
                        {
                            return (new { exception = ex.Message, environment = _options.EnvironmentType.ToString(), succeeded = false }, "application/json", false);
                        }
                    }

                    #endregion

                    throw new ArgumentException($"Select -> Invalid endpoint query argument: {routeString}");
                }

                var endPointQuery = await _persistanceProvider.GetEndPointQuery(dataLinqRoute.EndpointId, dataLinqRoute.QueryId);
                if (endPointQuery == null)
                {
                    throw new ArgumentException($"Select -> Endpoint query not found: {dataLinqRoute.EndpointId}@{dataLinqRoute.QueryId}");
                }

                if (!await IsAllowed(httpContext, dataLinqRoute, endPoint, endPointQuery))
                {
                    throw new AuthenticationException($"Select -> Endpoint Query ({dataLinqRoute.QueryId}): {CurrentUsername(httpContext)} not authorized");
                }

                #region Query Domains

                Dictionary<string, Dictionary<string, string>> domains = new Dictionary<string, Dictionary<string, string>>();

                if (!isDomainQuery && endPointQuery.Domains != null)
                {
                    foreach (var domain in endPointQuery.Domains)
                    {
                        //string domainContentType;
                        //bool domainSucceeded;

                        if (!String.IsNullOrWhiteSpace(domain.DestinationField) &&
                            !String.IsNullOrWhiteSpace(domain.QueryId) &&
                            !String.IsNullOrWhiteSpace(domain.ValueField) &&
                            !String.IsNullOrWhiteSpace(domain.NameField))
                        {
                            string domainQueryId = domain.QueryId;
                            NameValueCollection domainArguments = new NameValueCollection();
                            if (domainQueryId.Contains("?"))
                            {
                                domainArguments = HttpUtility.ParseQueryString(domainQueryId.Substring(domainQueryId.IndexOf("?") + 1));
                                domainQueryId = domainQueryId.Split('?')[0];
                            }

                            var domainRoute = new DataLinqRoute(domainQueryId, null);
                            if (domainRoute.EndpointId == dataLinqRoute.EndpointId && dataLinqRoute.HasEndpointToken)
                            {
                                domainRoute.EndpointToken = dataLinqRoute.EndpointToken;
                            }

                            var domainResult = await QueryAsync(httpContext, domainRoute.ToString(), domainArguments, true);
                            var domainRecords = domainResult.result as object[];

                            //var domainRecords = await Query(domainQueryId, domainArguments, out domainContentType, out domainSucceeded, true) as object[];
                            if (domainRecords == null)
                            {
                                continue;
                            }

                            var domainDict = new Dictionary<string, string>();
                            foreach (var domainRecord in domainRecords)
                            {
                                var domainRecordDict = (IDictionary<string, object>)domainRecord;
                                // Wenn Wert = "" => Wert überspringen. Bei Wert: "" und Name: "alles" würde sonst überall "alles" angezeigt werden.
                                if (/*!String.IsNullOrWhiteSpace(domainRecordDict[domain.ValueField]?.ToString()) &&*/ domainRecordDict.ContainsKey(domain.ValueField) && domainRecordDict.ContainsKey(domain.NameField))
                                {
                                    domainDict.Add(domainRecordDict[domain.ValueField]?.ToString(), domainRecordDict[domain.NameField]?.ToString());
                                }
                            }
                            if (domainDict.Count > 0)
                            {
                                domains[domain.DestinationField] = domainDict;
                            }
                        }
                    }
                }

                #endregion

                var queryEngineResult = await datalinqQueryEngine.SelectAsync(endPoint, endPointQuery, arguments);
                var records = queryEngineResult.records;
                var isOrdered = queryEngineResult.isOrdered;

                if (isOrdered == false && !String.IsNullOrWhiteSpace(arguments["_orderby"]))
                {
                    IOrderedEnumerable<IDictionary<string, object>> recordsDict = null;
                    foreach (string orderField in arguments["_orderby"].Split(','))
                    {
                        if (recordsDict == null)
                        {
                            if (orderField.StartsWith("-"))
                            {
                                recordsDict = records.Select(r => (IDictionary<string, object>)r).OrderByDescending(r => DBNull.Value.Equals(r[orderField.Substring(1)]) ? null : r[orderField.Substring(1)]);
                            }
                            else
                            {
                                recordsDict = records.Select(r => (IDictionary<string, object>)r).OrderBy(r => DBNull.Value.Equals(r[orderField]) ? null : r[orderField]);
                            }
                        }
                        else
                        {
                            if (orderField.StartsWith("-"))
                            {
                                recordsDict = ((IOrderedEnumerable<IDictionary<string, object>>)recordsDict).ThenByDescending(r => DBNull.Value.Equals(r[orderField.Substring(1)]) ? null : r[orderField.Substring(1)]);
                            }
                            else
                            {
                                recordsDict = ((IOrderedEnumerable<IDictionary<string, object>>)recordsDict).ThenBy(r => DBNull.Value.Equals(r[orderField]) ? null : r[orderField]);
                            }
                        }
                    }

                    records = recordsDict?.ToArray();
                }

                if (domains.Count > 0)
                {
                    foreach (var destinationField in domains.Keys)
                    {
                        var domain = domains[destinationField];
                        foreach (var record in records)
                        {
                            var recordsDict = (IDictionary<string, object>)record;
                            if (recordsDict.ContainsKey(destinationField))
                            {
                                if (domain.ContainsKey(recordsDict[destinationField]?.ToString()))
                                {
                                    // Den Originalwert (wichtig für Select-Box Vorauswahl) im einem Feld speichern
                                    recordsDict[destinationField + "_ORIGINAL"] = recordsDict[destinationField];
                                    recordsDict[destinationField] = domain[recordsDict[destinationField]?.ToString()];
                                }
                            }
                        }
                    }
                }

                if (dataLinqRoute.HasView)
                {
                    if (dataLinqRoute.ViewId == "json")
                    {
                        contentType = "application/json";
                        // ToDo: Gibt es das überhaupt (hier wird als View anscheinend "json" übergeben...
                        //       Wird meiner Meinung im Controller als Rückgabetype IActionResult gar nicht abgefragt...
                        //return (result: Base.JsonObject(new SelectResult(httpContext.Request, startTime, records, ui)), contentType: contentType, succeeded: succeeded);

                        // Neu: gib einen String zurück
                        return (result: JsonConvert.SerializeObject(new SelectResult(httpContext,
                                                                                     _datalinqCompiler.RazorEngine,
                                                                                     this,
                                                                                     httpContext.Request,
                                                                                     startTime,
                                                                                     records,
                                                                                     _hostAuthentication?.GetUser(httpContext))),
                                                                                     contentType: contentType,
                                                                                     succeeded: succeeded);
                    }
                    else
                    {
                        (object result, string contentType) transformed = (null, null);

                        if (records?.Length > 0)
                        {
                            var first = records[0];

                            if (first is IDictionary<string, object> dict &&
                                dict.TryGetValue("IsJsonApiResponse", out var val) &&
                                val is bool isMarker && isMarker)
                            {
                                records = records.Skip(1).ToArray();
                            }
                            else
                            {
                                transformed = _selectResultProviders.TransformAny(
                                    dataLinqRoute.ViewId,
                                    records.Select(r => (IDictionary<string, object>)r).ToArray()
                                );
                            }
                        }


                        if (transformed.result != null)
                        {
                            return (result: transformed.result, contentType: transformed.contentType, succeeded: true);
                        }
                    }

                    var endPointQueryView = await _persistanceProvider.GetEndPointQueryView(dataLinqRoute.EndpointId, dataLinqRoute.QueryId, dataLinqRoute.ViewId);
                    if (endPointQueryView == null)
                    {
                        throw new ArgumentException("Endpoint query view not found");
                    }

                    string result = await _datalinqCompiler.RenderRazorView(
                                                            this,
                                                            httpContext,
                                                            endPointQueryView,
                                                            routeString,
                                                            records,
                                                            _hostAuthentication?.GetUser(httpContext),
                                                            startTime
                                                        );

                    contentType = "text/html";
                    return (result: result, contentType: contentType, succeeded: succeeded);
                }

                contentType = "application/json";
                return (result: records, contentType: contentType, succeeded: succeeded);
            }

            contentType = "";
            return (result: null, contentType: contentType, succeeded: succeeded);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Select");

            if (dataLinqRoute.HasView)
            {
                StringBuilder sb = new StringBuilder();
                bool showStackTrace = false;
#if DEBUG
                showStackTrace = true;
#endif
                sb.Append("<div style='background-color:#efefaa;display:inline-block;margin:5px;padding:10px;border:1px solid red'>");
                sb.Append(ex.Message);

                if (showStackTrace || ex is NullReferenceException)
                {
                    sb.Append("<br/><br/>");
                    sb.Append(ex.StackTrace.Replace("\n", "<br/>"));
                }
#if DEBUG
                var innerException = ex.InnerException;
                while (innerException != null)
                {
                    sb.Append("<br/><br/>");
                    sb.Append("Inner Exception: ");
                    sb.Append("<br/>");
                    sb.Append(innerException.Message);
                    sb.Append("<br/><br/>");
                    sb.Append(innerException.StackTrace.Replace("\n", "<br/>"));

                    innerException = innerException.InnerException;
                }
#endif
                sb.Append("</div>");

                contentType = "text/html";

                succeeded = false;
                return (result: sb.ToString(), contentType: contentType, succeeded: succeeded);
            }
            throw;
        }
    }

    async public Task<bool> ExecuteNonQueryAsync(HttpContext httpContext,
                                                 string routeString)
    {
        if (String.IsNullOrWhiteSpace(routeString))
        {
            throw new ArgumentException("Invalid/empty datalinq route");
        }

        var datalinqRoute = new DataLinqRoute(routeString, httpContext);

        if (!datalinqRoute.HasQuery)
        {
            throw new ArgumentException($"ExecuteNonQuery -> Invalid endpoint query argument: {routeString}");
        }

        var endPoint = await _persistanceProvider.GetEndPoint(datalinqRoute.EndpointId);
        if (endPoint == null)
        {
            throw new ArgumentException("Endpoint not found");
        }

        if (!await IsAllowed(httpContext, datalinqRoute, endPoint))
        {
            throw new AuthenticationException($"Endpoint ({datalinqRoute.EndpointId}): {CurrentUsername(httpContext)} not authorized");
        }

        var endPointQuery = await _persistanceProvider.GetEndPointQuery(datalinqRoute.EndpointId, datalinqRoute.QueryId);
        if (endPointQuery == null)
        {
            throw new ArgumentException("Endpoint query not found");
        }

        if (!await IsAllowed(httpContext, datalinqRoute, endPoint, endPointQuery))
        {
            throw new AuthenticationException($"Endpoint Query ({datalinqRoute.QueryId}): {CurrentUsername(httpContext)} not authorized");
        }

        var exeuteNoneQueryEngine = _dataLinqQueryEngines
                                            .Where(e => e.EndpointType == endPoint.TypeValue && e is IDataLinqExecuteNonQueryEngine)
                                            .FirstOrDefault() as IDataLinqExecuteNonQueryEngine;

        if (exeuteNoneQueryEngine == null)
        {
            throw new Exception($"IDataLinqExecuteNonQueryEngine is not supported for endpoint type {endPoint.TypeValue}");
        }

        return await exeuteNoneQueryEngine.ExecuteNonQueryAsync(endPoint, endPointQuery, httpContext.Request.Form.ToCollection());
    }

    async public Task<DataLinqEndPointQueryView> GetReportQueryView(HttpContext httpContext, string routeString)
    {
        DataLinqEndPointQueryView endPointQueryView = null;
        NameValueCollection parameters = new NameValueCollection();

        if (routeString == DataLinq.Core.Const.IndexViewId)
        {
            //endPointQueryView = _apiTools.ExecuteToolCommand<DataLinqIndex, DataLinqEndPointQueryView>(HttpContext, "get-index-view", parameters, String.Empty);
        }
        else
        {
            var datalinqRoute = new DataLinqRoute(routeString, httpContext);

            if (datalinqRoute.HasEndpointToken || datalinqRoute.HasQueryToken)
            {
                throw new ArgumentException("Endpoint/Query access tokens are not allowed in report url.");
            }

            endPointQueryView = await _persistanceProvider.GetEndPointQueryView(datalinqRoute.EndpointId, datalinqRoute.QueryId, datalinqRoute.ViewId);

            if (endPointQueryView != null && endPointQueryView.IncludedJsLibraries == null)
            {
                endPointQueryView.IncludedJsLibraries = JsLibrary.LegacyDefaultNames;  // Compatibility with older versions => there Chart.js was always loaded 
            }
        }

        return endPointQueryView;
    }

    async public Task<string> GetEndpointCss(string endPointId)
    {
        string css = String.Empty;

        if (_options.CssInPerstanceStorage == true)
        {
            css = await _persistanceProvider.GetEndPointCss(endPointId);
        }

        // Fallback: auch wenn shared Storage, dann locale Instanz durchsuchen
        if (String.IsNullOrEmpty(css))
        {
            var fi = new System.IO.FileInfo($"{_environment.WebRootPath}/content/datalinq/{endPointId}/datalinq.css");

            if (fi.Exists)
            {
                css = System.IO.File.ReadAllText(fi.FullName);
            }
        }

        return css;
    }

    async public Task<string> GetViewCss(string endPointId)
    {
        string css = String.Empty;

        if (_options.CssInPerstanceStorage == true)
        {
            css = await _persistanceProvider.GetViewCss(endPointId);
        }

        // Fallback: auch wenn shared Storage, dann locale Instanz durchsuchen
        if (String.IsNullOrEmpty(css))
        {
            var fi = new System.IO.FileInfo($"{_environment.WebRootPath}/content/datalinq/{endPointId}/datalinq.css");

            if (fi.Exists)
            {
                css = System.IO.File.ReadAllText(fi.FullName);
            }
        }

        return css;
    }

    async public Task<string> GetEndpointJavascript(string endPointId)
    {
        string js = String.Empty;

        if (_options.JavascriptInPerstanceStorage == true)
        {
            js = await _persistanceProvider.GetEndPointJavascript(endPointId);
        }

        return js;
    }

    async public Task<string> GetViewJs(string endPointId)
    {
        string js = String.Empty;

        if (_options.JavascriptInPerstanceStorage == true)
        {
            js = await _persistanceProvider.GetViewJs(endPointId);
        }

        return js;
    }

    private bool IsAllowed(IDataLinqUser datalinqUser, string[] access)
    {
        if (access == null)
        {
            return true;
        }

        if (!_accessControl.IsAllowed(datalinqUser == null ? "" : datalinqUser.Username, access) &&
            !_accessControl.IsAllowed(datalinqUser == null ? null : datalinqUser.Userroles.ToArray(), access))
        {
            return false;
        }

        return true;
    }

    private bool IsValidToken(string token, string[] accessTokens)
    {
        if (!String.IsNullOrEmpty(token) && accessTokens != null)
        {
            return accessTokens.Contains(token);
        }

        return false;
    }

    async private ValueTask<bool> IsAllowed(DataLinqCodeIdentity dataLinqCodeIdentity, string endPointId)
    {
        if (String.IsNullOrEmpty(dataLinqCodeIdentity?.Name))
        {
            return false;
        }

        return dataLinqCodeIdentity.HasEndPointRoleParameter(endPointId) ||
               dataLinqCodeIdentity.Name.Equals(await _persistanceProvider.EndPointCreator(endPointId), StringComparison.OrdinalIgnoreCase);
    }

    async private ValueTask<bool> IsAllowed(HttpContext httpContext,
                                 DataLinqRoute dataLinqRoute,
                                 DataLinqEndPoint endPoint)
            => await IsAllowed(httpContext,
                         dataLinqRoute.EndpointId,
                         await _dataLinqAccessProvider.GetAccess(endPoint),
                         dataLinqRoute.EndpointToken,
                         await _dataLinqAccessProvider.GetAccessTokens(endPoint));


    async private ValueTask<bool> IsAllowed(HttpContext httpContext,
                                 DataLinqRoute dataLinqRoute,
                                 DataLinqEndPoint endPoint,
                                 DataLinqEndPointQuery endPointQuery)
            => await IsAllowed(httpContext,
                         dataLinqRoute.EndpointId,
                         await _dataLinqAccessProvider.GetAccess(endPoint, endPointQuery),
                         dataLinqRoute.QueryToken,
                         await _dataLinqAccessProvider.GetAccessTokens(endPointQuery));


    async private ValueTask<bool> IsAllowed(HttpContext httpContext,
                                       string endPointId,
                                       string[] access,
                                       string token,
                                       string[] accessToken)
    {
        IDataLinqUser dataLinqUser = _hostAuthentication?.GetUser(httpContext);
        DataLinqCodeIdentity dataLinqCodeIdentity = _dataLinqCodeIdentity?.CurrentIdentity();

        return
            IsAllowed(dataLinqUser, access) ||
            IsValidToken(token, accessToken) ||
            await IsAllowed(dataLinqCodeIdentity, endPointId);
    }

    private string CurrentUsername(HttpContext httpContext)
    {
        IDataLinqUser dataLinqUser = _hostAuthentication?.GetUser(httpContext);
        DataLinqCodeIdentity dataLinqCodeIdentity = _dataLinqCodeIdentity?.CurrentIdentity();

        if (dataLinqUser != null)
        {
            return dataLinqUser.Username;
        }

        if (dataLinqCodeIdentity != null)
        {
            return dataLinqCodeIdentity.Name;
        }

        return String.Empty;
    }


    public bool ClearCachedResults()
    {
        bool result = true;

        if (_dataLinqQueryEngines != null)
        {
            foreach (IDataLinqEngineCache datalinqQueryEngine in _dataLinqQueryEngines.Where(e => e is IDataLinqEngineCache))
            {
                result &= datalinqQueryEngine.ClearCache();
            }
        }

        return result;
    }
}
