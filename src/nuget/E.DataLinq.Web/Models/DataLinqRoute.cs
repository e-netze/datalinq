using E.DataLinq.Web.Extensions;
using Microsoft.AspNetCore.Http;
using System;
using System.Text;

namespace E.DataLinq.Web.Models;

class DataLinqRoute
{
    public DataLinqRoute(string routeString, HttpContext httpContext)
    {
        if (String.IsNullOrEmpty(routeString))
        {
            throw new ArgumentException("Invalid/empty datalinq route");
        }

        var ids = routeString.Split('@');

        var endPointIdResult = ParseId(ids[0]);
        this.EndpointId = endPointIdResult.id;
        this.EndpointToken = endPointIdResult.token;

        if (ids.Length > 1)
        {
            var queryIdResult = ParseId(ids[1]);
            this.QueryId = queryIdResult.id;
            this.QueryToken = queryIdResult.token;
        }

        if (ids.Length > 2)
        {
            this.ViewId = ids[2];
        }

        if (httpContext?.Request != null)
        {
            if (String.IsNullOrEmpty(this.EndpointToken))
            {
                this.EndpointToken = httpContext.Request.QueryOrForm("endpoint_token");
            }
            if (String.IsNullOrEmpty(this.QueryToken))
            {
                this.QueryToken = httpContext.Request.QueryOrForm("query_token");
            }
        }
    }

    public string EndpointId { get; set; }
    public string QueryId { get; set; }
    public string ViewId { get; set; }

    public string EndpointToken { get; set; }
    public string QueryToken { get; set; }

    public bool HasEndpoint => !string.IsNullOrEmpty(this.EndpointId);
    public bool HasQuery => !string.IsNullOrEmpty(this.QueryId);
    public bool HasView => !string.IsNullOrEmpty(this.ViewId);

    public bool HasEndpointToken => !string.IsNullOrEmpty(this.EndpointToken);
    public bool HasQueryToken => !string.IsNullOrEmpty(this.QueryToken);

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();

        if (!String.IsNullOrEmpty(this.EndpointId))
        {
            sb.Append(this.EndpointId);
            if (!String.IsNullOrEmpty(this.EndpointToken))
            {
                sb.Append($"({this.EndpointToken})");
            }

            if (!String.IsNullOrEmpty(this.QueryId))
            {
                sb.Append($"@{this.QueryId}");
                if (!String.IsNullOrEmpty(this.QueryToken))
                {
                    sb.Append($"({this.QueryToken})");
                }

                if (!String.IsNullOrEmpty(this.ViewId))
                {
                    sb.Append($"@{this.ViewId}");
                }
            }
        }

        return sb.ToString();
    }

    #region Helper

    private (string id, string token) ParseId(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return (null, null);
        }

        id = id.Trim();

        if (id.Contains("(") && id.EndsWith(")"))
        {
            int pos = id.IndexOf("(");

            return (id.Substring(0, pos), id.Substring(pos + 1, id.Length - pos - 2));
        }

        return (id, null);
    }

    #endregion
}
