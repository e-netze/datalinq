using E.DataLinq.Core.Services.Abstraction;
using Microsoft.AspNetCore.Http;

namespace E.DataLinq.Code.Services;

class DefaultHostUrlHelper : IHostUrlHelper
{
    private readonly HttpRequest _request;

    public DefaultHostUrlHelper(IHttpContextAccessor httpContextAccessor)
    {
        if (httpContextAccessor?.HttpContext != null)
        {
            _request = httpContextAccessor.HttpContext.Request;
        }
    }

    public string HostAppRootUrl()
    {
        var host = _request.Host.ToUriComponent();

        var pathBase = _request.PathBase.ToUriComponent();

        return $"{_request.Scheme}://{host}{pathBase}";
    }
}
