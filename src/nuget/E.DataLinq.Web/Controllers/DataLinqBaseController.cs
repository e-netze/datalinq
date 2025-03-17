using E.DataLinq.Core.Services.Abstraction;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Controllers;

public class DataLinqBaseController : Controller
{
    private readonly IHostUrlHelper _hostUrlHelper;
    protected DataLinqBaseController(IHostUrlHelper hostUrlHelper)
    {
        _hostUrlHelper = hostUrlHelper;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        string appRootUrl = _hostUrlHelper.HostAppRootUrl();

        if (String.IsNullOrEmpty(appRootUrl))
        {
            appRootUrl = this.Url.Content("~");
        }

        this.ViewData["appRootUrl"] = appRootUrl;
        this.ViewData["contentRootUrl"] = $"{appRootUrl}/_content/E.DataLinq.Web";
    }

    public string Title { get { return ViewBag.Title; } set { ViewBag.Title = value; } }

    protected Task<IActionResult> JsonObject(object obj, bool pretty = false)
    {
        MemoryStream ms = new MemoryStream();

        var jw = new Newtonsoft.Json.JsonTextWriter(new StreamWriter(ms));
        jw.Formatting = pretty ?
            Newtonsoft.Json.Formatting.Indented :
            Newtonsoft.Json.Formatting.None;
        var serializer = new Newtonsoft.Json.JsonSerializer();
        serializer.Serialize(jw, obj);
        jw.Flush();
        ms.Position = 0;

        string json = System.Text.Encoding.UTF8.GetString(ms.GetBuffer());
        json = json.Trim('\0');

        return Task.FromResult<IActionResult>(JsonResultStream(json));
    }

    async protected Task<IActionResult> JsonViewSuccess(bool success, string exceptionMessage = "", string exceptionType = "", string requestId = null)
    {
        if (!success && !String.IsNullOrEmpty(exceptionMessage))
        {
            return await JsonObject(new JsonException()
            {
                success = success,
                exception = exceptionMessage,
                exception_type = exceptionType,
                requestid = requestId
            });
        }
        return JsonResultStream("{\"success\":" + success.ToString().ToLower() + "}");
    }

    protected IActionResult RawResponse(byte[] responseBytes, string contentType, NameValueCollection headers)
    {
        if (headers != null)
        {
            foreach (string header in headers)
            {
                this.Response.Headers.Append(header, headers[header]);
            }
        }

        try
        {
            Response.Headers.Append("Access-Control-Allow-Headers", "*");
            Response.Headers.Append("Access-Control-Allow-Origin", (string)Request.Headers["Origin"] != null ? (string)Request.Headers["Origin"] : "*");
            Response.Headers.Append("Access-Control-Allow-Credentials", "true");
        }
        catch { }

        return BinaryResultStream(responseBytes, contentType);
    }

    protected IActionResult ViewResult(object model = null) => View(model);
    protected IActionResult ViewResult(string viewName, object model = null) => View(viewName, model);

    #region Return Streams 

    protected IActionResult BinaryResultStream(byte[] data, string contentType, string fileName = "")
    {
        if (!String.IsNullOrWhiteSpace(fileName))
        {
            Response.Headers.Append("Content-Disposition", "attachment; filename=\"" + fileName + "\"");
        }

        return File(data, contentType);
    }

    protected IActionResult JsonResultStream(string json)
    {
        json = json ?? String.Empty;

        Response.Headers.Append("Pragma", "no-cache");
        Response.Headers.Append("Cache-Control", "no-cache, no-store, max-age=0, must-revalidate");
        Response.Headers.Append("Access-Control-Allow-Headers", "*");
        Response.Headers.Append("Access-Control-Allow-Origin", (string)Request.Headers["Origin"] != null ? (string)Request.Headers["Origin"] : "*");
        Response.Headers.Append("Access-Control-Allow-Credentials", "true");

        return BinaryResultStream(Encoding.UTF8.GetBytes(json), "application/json; charset=utf-8");
    }

    protected IActionResult PlainResultStream(string text, string contantType)
    {
        text = text ?? String.Empty;

        return BinaryResultStream(Encoding.UTF8.GetBytes(text), contantType);
    }

    #endregion

    #region Classes

    private class JsonResponse
    {
        public bool success { get; set; }
    }

    private class JsonException : JsonResponse
    {
        public string exception { get; set; }
        public string exception_type { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string requestid { get; set; }
    }

    #endregion
}
