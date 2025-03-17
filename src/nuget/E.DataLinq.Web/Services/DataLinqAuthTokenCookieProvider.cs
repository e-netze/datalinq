using E.DataLinq.Core.Services.Crypto.Abstraction;
using E.DataLinq.Web.Services.Abstraction;
using Microsoft.AspNetCore.Http;
using System;

namespace E.DataLinq.Web.Services;

class DataLinqAuthTokenCookieProvider : IDataLinqAccessTokenAuthProvider
{
    private const string CookieName = "datalinq-code-auth";

    private readonly ICryptoService _crypto;
    private readonly HttpContext _httpContext;

    public DataLinqAuthTokenCookieProvider(ICryptoService crypto,
                                     IHttpContextAccessor httpContextAccessor)
    {
        _crypto = crypto;
        _httpContext = httpContextAccessor.HttpContext;
    }

    public void SetAccessToken(string accessToken)
    {
        _httpContext.Response.Cookies.Append(CookieName,
            _crypto.EncryptTextDefault($"{accessToken}"));
    }

    public string GetAccessToken()
    {
        var cookieValue = _httpContext.Request.Cookies[CookieName];
        if (String.IsNullOrEmpty(cookieValue))
        {
            return String.Empty;
        }

        try
        {
            cookieValue = _crypto.DecryptTextDefault(cookieValue);

            return cookieValue;
        }
        catch { return String.Empty; }
    }

    public void DeleteToken()
    {
        _httpContext.Response.Cookies.Delete(CookieName);
    }
}
