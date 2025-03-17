using E.DataLinq.Core.Exceptions;
using E.DataLinq.Core.Security.Token;
using E.DataLinq.Core.Security.Token.Models;
using E.DataLinq.Core.Services.Abstraction;
using E.DataLinq.Web.Reflection;
using E.DataLinq.Web.Services;
using E.DataLinq.Web.Services.Abstraction;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Middleware.Authentication;

public class DataLinqAccessTokenAuthenticationMiddleware
{
    private readonly ILogger<DataLinqAccessTokenAuthenticationMiddleware> _logger;
    private readonly RequestDelegate _next;
    private readonly AccessTokenService _accessTokenService;
    private readonly DataLinqCodeApiOptions _codeApiOptions;

    public DataLinqAccessTokenAuthenticationMiddleware(ILogger<DataLinqAccessTokenAuthenticationMiddleware> logger,
                                                       RequestDelegate next,
                                                       AccessTokenService accessTokenService,
                                                       IOptions<DataLinqCodeApiOptions> options)
    {
        _logger = logger;
        _next = next;
        _accessTokenService = accessTokenService;
        _codeApiOptions = options.Value;
    }

    public async Task InvokeAsync(HttpContext httpContext,
                                  IHostUrlHelper urlHelper,
                                  IEnumerable<IDataLinqAccessTokenAuthProvider> tokenProviders,
                                  IEnumerable<IRoutingEndPointReflectionProvider> endPointReflectionProviders)
    {
        bool applyMiddleware = true;

        if (endPointReflectionProviders != null && endPointReflectionProviders.Count() > 0)
        {
            applyMiddleware = false;

            foreach (var provider in endPointReflectionProviders)
            {
                var attribute =
                    provider?.GetActionMethodCustomAttribute<HostAuthenticationAttribute>() ??
                    provider?.GetControllerCustomAttribute<HostAuthenticationAttribute>();

                if (attribute != null &&
                    attribute.AuthenticationType.HasFlag(HostAuthenticationTypes.DataLinqAccessToken))
                {
                    applyMiddleware = true;
                }
            }
        }

        if (applyMiddleware)
        {
            string accessTokenString = tokenProviders
                .Select(p => p.GetAccessToken())
                .Where(t => !String.IsNullOrEmpty(t))
                .FirstOrDefault();

            if (!String.IsNullOrEmpty(accessTokenString))
            {
                try
                {
                    var accessToken = _accessTokenService.CreateAccessToken(accessTokenString);
                    var accessTokenIssuer = _codeApiOptions.AccessTokenIssuer ?? urlHelper.HostAppRootUrl();

                    if (!accessTokenIssuer.Equals(accessToken?.Payload?.iis, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidTokenException($"wrong issuer url: {accessToken?.Payload?.iis} != {accessTokenIssuer}");
                    }

                    if (accessToken?.Payload != null &&
                        !String.IsNullOrEmpty(accessToken.Payload.name))
                    {
                        List<Claim> claims = new List<Claim>();

                        if (accessToken.Payload.roles != null)
                        {
                            foreach (var role in accessToken.Payload.roles)
                            {
                                claims.Add(new Claim(AuthConst.RolesCaimType, role));
                            }
                        }

                        if (!String.IsNullOrEmpty(accessToken.Payload.sub))
                        {
                            claims.Add(new Claim(AuthConst.SubClaimType, accessToken.Payload.sub));
                        }

                        var claimsIdentity = new ClaimsIdentity(new Identity(accessToken.Payload), claims);
                        var claimsPricipal = new ClaimsPrincipal(claimsIdentity);

                        httpContext.User = claimsPricipal;
                    }
                }
                catch (InvalidTokenException ex)
                {
                    _logger.LogWarning(ex, "Invalid token");
                }
            }
        }

        await _next.Invoke(httpContext);
    }

    #region Helper Class

    private class Identity : IIdentity
    {
        public Identity(Payload payload)
        {
            this.Name = payload.name;
            _isAuthenticated = !String.IsNullOrEmpty(payload.name);

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
}
