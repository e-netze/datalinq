namespace E.DataLinq.Web.Services;

static internal class DataLinqErrorPageDefaults
{
    public const string DefaultRazorCode =
"""
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
