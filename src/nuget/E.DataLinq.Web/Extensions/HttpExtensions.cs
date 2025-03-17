using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Extensions;

static class HttpExtensions
{
    static public string QueryOrForm(this HttpRequest request, string key, string defaultValue = "")
    {
        // Sollte nie "null" zurück geben

        if (!String.IsNullOrEmpty(request.Query[key]))
        {
            return request.Query[key];
        }

        return request.HasFormContentType &&
               request.Form != null &&
               !String.IsNullOrWhiteSpace(request.Form[key]) ? request.Form[key].ToString() : String.Empty;
    }

    async static public Task<T> FromBody<T>(this HttpRequest request)
    {
        request.EnableBuffering();

        // Leave the body open so the next middleware can read it.
        using (var reader = new StreamReader(request.Body,
                                             encoding: Encoding.UTF8,
                                             detectEncodingFromByteOrderMarks: false,
                                             bufferSize: 1024,
                                             leaveOpen: true))
        {
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            return JsonConvert.DeserializeObject<T>(body);
        }
    }

    static public string DisplayUrl(this HttpContext httpContext)
    {
        var request = httpContext.Request;
        var scheme = httpContext.Request.Scheme;

        var xProtoHeader = httpContext.Request.Headers["X-Forwarded-Proto"].ToString();
        if (xProtoHeader != null && xProtoHeader.StartsWith("https", StringComparison.OrdinalIgnoreCase))
        {
            scheme = "https";
        }

        return $"{scheme}://{request.Host}{request.PathBase.ToUriComponent()}{request.Path}";
    }
}
