using E.DataLinq.Core.Models.Authentication;
using E.DataLinq.Core.Security.Token;
using E.DataLinq.Core.Security.Token.Models;
using E.DataLinq.Core.Services.Abstraction;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Models.Auth;
using E.DataLinq.Web.Services;
using E.DataLinq.Web.Services.Abstraction;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace E.DataLinq.Web.Controllers;

public class DataLinqAuthController : DataLinqBaseController
{
    private readonly IHostUrlHelper _urlHelper;
    private readonly AccessTokenService _tokenService;
    private readonly IEnumerable<IDataLinqCodeIdentityProvider> _identityProviders;
    private readonly IEnumerable<IDataLinqAccessTokenAuthProvider> _tokenProviders;
    private readonly DataLinqCodeApiOptions _codeApiOptions;

    public DataLinqAuthController(IHostUrlHelper urlHelper,
                                  AccessTokenService tokenService,
                                  IEnumerable<IDataLinqCodeIdentityProvider> identityProviders,
                                  IEnumerable<IDataLinqAccessTokenAuthProvider> tokenProviders,
                                  IOptions<DataLinqCodeApiOptions> options)
        : base(urlHelper)
    {
        _urlHelper = urlHelper;
        _tokenService = tokenService;
        _identityProviders = identityProviders;
        _tokenProviders = tokenProviders;
        _codeApiOptions = options.Value;
    }

    public IActionResult Index(string redirect)
    {
        try
        {
            if (_identityProviders.Count() == 0)
            {
                throw new Exception("DataLinq.Code.Api is not implemented with this DataLinq instance.");
            }

            if (String.IsNullOrEmpty(redirect))
            {
                throw new Exception("Parameter redirect required.");
            }

            _codeApiOptions.CheckDataLinqCodeClientUrl(redirect);

            var identity = _identityProviders
                    .Select(provider => provider.TryGetIdentity(Request.Query))
                    .Where(identity => identity != null)
                    .FirstOrDefault();

            if (identity != null)
            {
                return Redirect(GetRedirectUrl(identity, redirect));
            }

            ViewData["background-color"] = "#888";

            return View(new LoginModel()
            {
                Redirect = redirect
            });
        }
        catch (Exception ex)
        {
            return RedirectToAction("Index", "DataLinq", new { error = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Index(LoginModel model)
    {
        try
        {
            _codeApiOptions.CheckDataLinqCodeClientUrl(model.Redirect);

            var identity = _identityProviders
                .Select(provider => provider.TryGetIdentity(model.Name, model.Password))
                .Where(identity => identity != null)
                .FirstOrDefault();

            if (identity == null)
            {
                throw new Exception("Invalid user or password");
            }

            return Redirect(GetRedirectUrl(identity, model.Redirect));

        }
        catch (Exception ex)
        {
            ViewData["background-color"] = "#888";
            model.ErrorMessage = ex.Message;

            return View(model);
        }
    }

    public IActionResult Logout(string redirect)
    {
        foreach (var tokenProvider in _tokenProviders)
        {
            tokenProvider.DeleteToken();
        }

        if (!String.IsNullOrEmpty(redirect))
        {
            return Redirect(redirect);
        }

        return RedirectToAction("Index");
    }

    #region Helper

    private string GetRedirectUrl(DataLinqCodeIdentity identity, string redirect)
    {
        var accessToken = _tokenService.CreateAccessToken(
            new Header()
            {
                alg = "",
                typ = "datalinq-token"
            },
            new Payload(3600 * 24)
            {
                iis = _codeApiOptions.AccessTokenIssuer ?? _urlHelper.HostAppRootUrl(),
                name = identity.Name,
                roles = identity.Roles.Where(r => r.Contains("datalinq")).ToArray(),
                sub = identity.Id
            }
        );

        foreach (var tokenProvider in _tokenProviders)
        {
            tokenProvider.SetAccessToken(accessToken.ToTokenString());
        }

        return $"{redirect}?accesstoken={HttpUtility.UrlEncode(accessToken.ToTokenString())}&userDisplayName={HttpUtility.UrlEncode(identity.Name)}";
    }

    #endregion
}
