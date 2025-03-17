using E.DataLinq.Code.Extensions;
using E.DataLinq.Code.Services;
using E.DataLinq.Core.Services.Crypto.Abstraction;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;

namespace E.DataLinq.Code.Middleware.Authentication;
internal class DataLinqCodeTokenAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public DataLinqCodeTokenAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext, ICryptoService crypto)
    {
        try
        {
            var token = !String.IsNullOrEmpty(httpContext.Request.Headers.Authorization)
                        && httpContext.Request.Headers.Authorization.ToString().StartsWith("Bearer ")
                        ? httpContext.Request.Headers.Authorization.ToString().Substring("Bearer ".Length)
                        : httpContext.Request.Query["dl_token"].ToString();

            var sessionData = crypto.GetSessionData(token);

            List<Claim> claims = [
                    new Claim(DataLinqCodeIndentityService.InstanceIdClaimType, sessionData[0]),
                    new Claim(DataLinqCodeIndentityService.AccessTokenClaimTyp, sessionData[2])
                ];

            var claimsIdentity = new ClaimsIdentity(new Identity(sessionData[1]), claims);
            var claimsPricipal = new ClaimsPrincipal(claimsIdentity);

            httpContext.User = claimsPricipal;
        }
        catch
        {

        }

        await _next(httpContext);
    }

    #region Helper Classes

    #region Helper Class

    private class Identity : IIdentity
    {
        public Identity(string name)
        {
            this.Name = name;
            _isAuthenticated = !String.IsNullOrEmpty(name);

            if (_isAuthenticated)
            {
                _authenticationType = "AuthenticationTypes.Federation";
            }
        }

        private readonly string _authenticationType;
        public string AuthenticationType => _authenticationType;

        private readonly bool _isAuthenticated = false;
        public bool IsAuthenticated => _isAuthenticated;

        public string Name { get; }
    }

    #endregion

    #endregion
}
