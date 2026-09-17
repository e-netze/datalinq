namespace E.DataLinq.Web.Services;

static internal class DataLinqErrorPageDefaults
{
    public const string DefaultRazorCode =
"""
@*
    ==========================================================================
    DataLinq error page - available helpers / variables
    ==========================================================================

    Injected globals (usable directly, without @Model):
        @exception            the thrown Exception (alias: @ex)
        @exception?.Message   the exception message
        @exception?.StackTrace the stack trace
        @queryString          the request query string (NameValueCollection)
                              e.g. @queryString["id"]

    Model properties (@Model.*):
        @Model.Exception         the thrown Exception (same as @exception)
        @Model.Timestamp         DateTime when the error occurred
        @Model.QueryString       request query string (NameValueCollection)
        @Model.Route             the DataLinq route (endpoint@query@view)
        @Model.EndpointId        the endpoint id
        @Model.QueryId           the query id
        @Model.ViewId            the view id
        @Model.RequestPath       the requested path
        @Model.IsDevelopment     true when running in a development environment
        @Model.ShowStackTrace    true if the stack trace should be shown
                                 (development or NullReferenceException)
        @Model.AllMessages       messages of the exception + all inner exceptions
        @Model.InnerExceptions   all inner exceptions (flattened)
        @Model.RootException     the innermost (root cause) exception

    Iterating over query parameters:
        @foreach (string key in Model.QueryString)
        {
            <div>@key = @Model.QueryString[key]</div>
        }
    ==========================================================================
*@
<div id='datalinq-services-error' style='background-color:#efefaa;display:inline-block;margin:5px;padding:10px;border:1px solid red'>
    <div>@exception?.Message</div>

    @if (Model.ShowStackTrace)
    {
        <pre style='white-space:pre-wrap;margin:10px 0 0 0'>@exception?.StackTrace</pre>

        foreach (var inner in Model.InnerExceptions)
        {
            <div style='margin-top:10px'><b>Inner Exception:</b></div>
            <div>@inner.Message</div>
            <pre style='white-space:pre-wrap;margin:5px 0 0 0'>@inner.StackTrace</pre>
        }
    }
</div>
""";
}
