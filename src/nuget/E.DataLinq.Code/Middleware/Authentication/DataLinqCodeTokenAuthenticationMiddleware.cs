using E.DataLinq.Code.Extensions;
using E.DataLinq.Code.Services;
using E.DataLinq.Core.Services.Crypto.Abstraction;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;

namespace E.DataLinq.Code.Middleware.Authentication;
internal class DataLinqCodeTokenAuthenticationMiddleware
{
    private readonly ILogger<DataLinqCodeTokenAuthenticationMiddleware> _logger;
    private readonly RequestDelegate _next;

    public DataLinqCodeTokenAuthenticationMiddleware(
            ILogger<DataLinqCodeTokenAuthenticationMiddleware> logger,
            RequestDelegate next)
    {
        _logger = logger;
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext httpContext,
        ICryptoService crypto)
    {
        try
        {
            var token = !String.IsNullOrEmpty(httpContext.Request.Headers.Authorization)
                        && httpContext.Request.Headers.Authorization.ToString().StartsWith("Bearer ")
                        ? httpContext.Request.Headers.Authorization.ToString().Substring("Bearer ".Length)
                        : httpContext.Request.Query["dl_token"].ToString();

            if (String.IsNullOrEmpty(token))
            {
                await _next(httpContext);
                return;
            }

            var sessionData = crypto.GetSessionData(token);

            if (sessionData.Length < 3)
            {
                throw new Exception($"Invalid session data");
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("DataLinq Identity {identity}", sessionData[1]);
                _logger.LogDebug("Claim - {claim}={claimvalue}", DataLinqCodeIndentityService.InstanceIdClaimType, sessionData[0]);
                _logger.LogDebug("Claim - {claim}={claimvalue}", DataLinqCodeIndentityService.AccessTokenClaimTyp, sessionData[2]);
            }

            int instanceId = 0;
            int.TryParse(sessionData[0], out instanceId);  // default is "0"
                                                           // if empty, use 0 
                                                           // otherwise webgiscloud is not working!

            List<Claim> claims = [
                    new Claim(DataLinqCodeIndentityService.InstanceIdClaimType, instanceId.ToString()),
                    new Claim(DataLinqCodeIndentityService.AccessTokenClaimTyp, sessionData[2])
                ];

            var claimsIdentity = new ClaimsIdentity(new Identity(sessionData[1]), claims);
            var claimsPricipal = new ClaimsPrincipal(claimsIdentity);

            httpContext.User = claimsPricipal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Authentication Warning: {auth-error-message}", ex.Message);
            httpContext.User = null;
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
