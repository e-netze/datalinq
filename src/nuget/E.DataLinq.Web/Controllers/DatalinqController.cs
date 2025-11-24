using E.DataLinq.Core.Models;
using E.DataLinq.Core.Services.Abstraction;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Models;
using E.DataLinq.Web.Razor;
using E.DataLinq.Web.Reflection;
using E.DataLinq.Web.Services;
using E.DataLinq.Web.Services.Abstraction;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Controllers;

[HostAuthentication(HostAuthenticationTypes.DataLinqEngine | HostAuthenticationTypes.DataLinqAccessToken)]
public class DataLinqController : DataLinqBaseController
{
    private readonly ILogger<DataLinqController> _logger;
    private readonly DataLinqService _datalinq;
    private readonly IEnumerable<IDataLinqCustomSelectArgumentsProvider> _customArgumentProviders;
    private readonly IHostAuthenticationService _hostAuthentication;
    private readonly IDataLinqCodeIdentityService _dataLinqCodeIdentity;
    private readonly IDataLinqLogger _datalinqLogger;

    public DataLinqController(ILogger<DataLinqController> logger,
                              DataLinqService datalinq,
                              IHostUrlHelper hostUrlHelper,
                              IEnumerable<IDataLinqCustomSelectArgumentsProvider> customArgumentProviders,
                              IHostAuthenticationService hostAuthenication = null,
                              IDataLinqCodeIdentityService dataLinqCodeIdentity = null,
                              IDataLinqLogger datalinqLogger = null)
        : base(hostUrlHelper)
    {
        _logger = logger;
        _datalinq = datalinq;
        _customArgumentProviders = customArgumentProviders;
        _hostAuthentication = hostAuthenication;
        _dataLinqCodeIdentity = dataLinqCodeIdentity;
        _datalinqLogger = datalinqLogger ?? new DataLinqNullLogger();
    }

    public IActionResult Index() => ViewResult();

    async public Task<IActionResult> Select(string __dataLinqRoute)
    {
        try
        {
            string userName =
                _hostAuthentication?.GetUser(this.HttpContext)?.Username ??
                _dataLinqCodeIdentity?.CurrentIdentity()?.Name ??
                String.Empty;

            using (var performanceLogger = _datalinqLogger.CreatePerformanceLogger(this.HttpContext, "select", __dataLinqRoute, userName))
            {
                object result = null;
                string contentType = String.Empty;
                bool succeeded = true;

                if (__dataLinqRoute == DataLinq.Core.Const.IndexViewId)
                {
                    // ToDo:
                    //var indexView = _apiTools.ExecuteToolCommand<DataLinqIndex, DataLinqEndPointQueryView>(HttpContext, "get-index-view", null, String.Empty);
                    //result = _datalinqCompiler.RenderRazorView(HttpContext, indexView, DataLinqIndex.IndexViewId, null, ui, DateTime.Now);
                    //contentType = "text/html";
                    //succeeded = true;
                }
                else
                {
                    var arguments = Request.Query.ToCollection();

                    foreach (var customArgumentProvider in _customArgumentProviders)
                    {
                        if (!String.IsNullOrEmpty(customArgumentProvider.ExclusivePrefix))
                        {
                            arguments = arguments.RemoveKeysStartsWith(customArgumentProvider.ExclusivePrefix);
                        }
                        arguments = arguments.Union(customArgumentProvider.CustomArguments(), customArgumentProvider.OverrideExisting);
                    }

                    var queryResult = await _datalinq.QueryAsync(HttpContext, __dataLinqRoute, arguments);
                    result = queryResult.result;
                    contentType = queryResult.contentType;
                    succeeded = queryResult.succeeded;
                }

                if (result is string)
                {
                    if (Request.Query["_f"] == "json")
                    {
                        return await JsonObject(new { html = result, _id = Request.Query["_id"], success = succeeded }, Request.Query["_pjson"] == "true");
                    }
                    return RawResponse(Encoding.UTF8.GetBytes((string)result), contentType, null);
                }
                else if (result is object[] && Request.Query["_f"] == "csv")
                {
                    string csv = ((object[])result).ExpandoToCsv(header: true);
                    var data = Encoding.UTF8.GetBytes(csv);
                    var dataBOM = Encoding.UTF8.GetPreamble().Concat(data).ToArray();

                    return RawResponse(dataBOM, "text/csv", new NameValueCollection { { "Content-Disposition", "attachment;filename=export.csv" } });
                }
                else if (result is object[] arr && arr.Length > 0)
                {
                    var first = arr[0];

                    if (first is IDictionary<string, object> dict &&
                        dict.TryGetValue("IsJsonApiResponse", out var val) &&
                        val is bool isMarker && isMarker)
                    {
                        var newArr = arr.Skip(1).ToArray();
                        return await JsonObjectNew(newArr, Request.Query["_pjson"] == "true");
                    }
                }


                return await JsonObject(result, Request.Query["_pjson"] == "true");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Select");

            return await JsonViewSuccess(false, ex.Message);
        }
    }

    async public Task<IActionResult> Report(string __dataLinqRoute, string _orderby = "")
    {
        try
        {
            string clientId = _hostAuthentication != null ? await _hostAuthentication.ClientSideAuthObjectStringAsync(HttpContext) : null,
                   authIntialText = "Authenticate...";

            if (String.IsNullOrEmpty(clientId) && _dataLinqCodeIdentity?.CurrentIdentity() != null)
            {
                authIntialText = $"DataLinq.Code: {_dataLinqCodeIdentity.CurrentIdentity().Name}";
            }

            var endPointQueryView = await _datalinq.GetReportQueryView(HttpContext, __dataLinqRoute);
            if (endPointQueryView == null)
            {
                throw new ArgumentException($"Unknown view {__dataLinqRoute}");
            }

            this.Title = endPointQueryView.Name;

            string userName =
                _hostAuthentication?.GetUser(this.HttpContext)?.Username ??
                _dataLinqCodeIdentity?.CurrentIdentity()?.Name ??
                String.Empty;

            using (var performanceLogger = _datalinqLogger.CreatePerformanceLogger(this.HttpContext, "report", __dataLinqRoute, userName))
            {
                return ViewResult("Report", new ReportModel()
                {
                    Id = __dataLinqRoute,
                    QueryString = Request.Query.ToCollection().Clone(new string[] { "_orderby" }).ToFilterString(),
                    OrderBy = _orderby,
                    ClientSideAuthObjectString = !String.IsNullOrEmpty(clientId) ? clientId : "null",
                    AuthIntialText = authIntialText,
                    IncludedJsLibraries = (endPointQueryView.IncludedJsLibraries ?? JsLibrary.LegacyDefaultNames).Split(','),
                    PDFReportMode = endPointQueryView.PDFReportMode
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Report");

            return await JsonViewSuccess(false, ex.Message);
        }
    }

    [HttpPost]
    async public Task<IActionResult> ExecuteNonQuery(string __dataLinqRoute)
    {

        try
        {
            if (!await _datalinq.ExecuteNonQueryAsync(HttpContext, __dataLinqRoute))
            {
                throw new Exception("Error in ExcuteNonQuery: Unknown Error");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteNonQuery");

            return await JsonViewSuccess(false, ex.Message);
        }

        return await JsonViewSuccess(true);
    }

    public IActionResult Help()
    {
        var language = Request.Query["lang"].ToString().DefaultIfNullOrEmpty("en");

        var model = new HelpModel();
        model.SelectedLanguage = language;

        //model.Classes.Add(ClassHelp.FromTypeUseAttributes(typeof(DataLinqHelper)));
        model.Classes.Add(ClassHelp.FromTypeUseXmlDocumentation(typeof(DataLinqHelper), language));
        model.Selected = Request.Query["selected"];

        return ViewResult(model);
    }

    async public Task<IActionResult> ClearCachedResults()
    {
        return await JsonObject(new { success = _datalinq.ClearCachedResults() });
    }

    async public Task<IActionResult> CssProxyEndpoint(string __dataLinqRoute)
    {
        // ToDo: Wird bei jedem Aufruf geladen
        // Irgend ein Caching/Dot Modified 304 Mechanismus?

        NameValueCollection parameters = new NameValueCollection();
        parameters.Add("endpoint", __dataLinqRoute);

        string css = await _datalinq.GetEndpointCss(__dataLinqRoute);

        return RawResponse(Encoding.UTF8.GetBytes(css ?? String.Empty), "text/css", new NameValueCollection());
    }

    async public Task<IActionResult> CssProxyView(string __dataLinqRoute)
    {
        NameValueCollection parameters = new NameValueCollection();
        parameters.Add("endpoint", __dataLinqRoute);

        string css = await _datalinq.GetViewCss(__dataLinqRoute);

        return RawResponse(Encoding.UTF8.GetBytes(css ?? String.Empty), "text/css", new NameValueCollection());
    }

    async public Task<IActionResult> JsProxyEndpoint(string __dataLinqRoute)
    {
        // ToDo: Wird bei jedem Aufruf geladen
        // Irgend ein Caching/Dot Modified 304 Mechanismus?

        NameValueCollection parameters = new NameValueCollection();
        parameters.Add("endpoint", __dataLinqRoute);

        string javascript = await _datalinq.GetEndpointJavascript(__dataLinqRoute);

        return RawResponse(Encoding.UTF8.GetBytes(javascript ?? String.Empty), "text/javascript", new NameValueCollection());
    }

    async public Task<IActionResult> JsProxyView(string __dataLinqRoute)
    {
        NameValueCollection parameters = new NameValueCollection();
        parameters.Add("endpoint", __dataLinqRoute);

        string javascript = await _datalinq.GetViewJs(__dataLinqRoute);

        return RawResponse(Encoding.UTF8.GetBytes(javascript ?? String.Empty), "text/javascript", new NameValueCollection());
    }
}
