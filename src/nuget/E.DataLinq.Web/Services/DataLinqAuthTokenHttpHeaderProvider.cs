using E.DataLinq.Web.Services.Abstraction;
using Microsoft.AspNetCore.Http;
using System;

namespace E.DataLinq.Web.Services;

class DataLinqAuthTokenHttpHeaderProvider : IDataLinqAccessTokenAuthProvider
{
    private const string AuthSchema = "datalinq-token ";
    private readonly IHttpContextAccessor _contextAccessor;

    public DataLinqAuthTokenHttpHeaderProvider(IHttpContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    public void DeleteToken()
    {
        // Do Nothing!
    }

    public string GetAccessToken()
    {
        string authHeader = _contextAccessor.HttpContext.Request.Headers["Authorization"];

        if (authHeader != null && authHeader.StartsWith(AuthSchema))
        {
            return authHeader.Substring(AuthSchema.Length);
        }

        return String.Empty;
    }

    public void SetAccessToken(string accessToken)
    {
        // Do Nothing!
    }
}
