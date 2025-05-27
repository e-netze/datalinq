using E.DataLinq.Code.Extensions;
using E.DataLinq.Code.Models;
using E.DataLinq.Code.Models.DataLinqCode;
using E.DataLinq.Code.Services;
using E.DataLinq.Core;
using E.DataLinq.Core.Exceptions;
using E.DataLinq.Core.Extensions;
using E.DataLinq.Core.Models;
using E.DataLinq.Core.Services.Abstraction;
using E.DataLinq.Core.Services.Crypto.Abstraction;
using E.DataLinq.Web.Api.Client;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace E.DataLinq.Code.Controllers;

public class DataLinqCodeController : DataLinqCodeBaseController
{
    private readonly CodeApiClient _client;
    private readonly DataLinqCodeService _dataLinqCode;
    private readonly IDataLinqAccessTreeService _accessTree;
    private readonly ICryptoService _crypto;

    public DataLinqCodeController(DataLinqCodeService dataLinqCode,
                                  IDataLinqAccessTreeService accessTree,
                                  ICryptoService crypto)
        : base()
    {
        _dataLinqCode = dataLinqCode;
        _client = _dataLinqCode.ApiClient;
        _accessTree = accessTree;
        _crypto = crypto;
    }

    public IActionResult Index()
    {
        if (_client == null || String.IsNullOrEmpty(_dataLinqCode.DataLinqEngineUrl))
        {
            return RedirectToAction("Index", "Home");
        }

        var currentUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase.ToUriComponent()}{Request.Path}";
        var userRoleParameters = _dataLinqCode.AccessTokenPayload?.roles.DataLinqCodeRoleParameters();

        return View(new IndexModel()
        {
            AccessToken = _dataLinqCode.AccessToken,
            InstanceName = _dataLinqCode.InstanceName,
            CurrentUrl = currentUrl,
            DataLinqEngineUrl = _dataLinqCode.DataLinqEngineUrl,
            CurrentUsername = _dataLinqCode.UserDisplayName,

            UseAppPrefixFilters = _dataLinqCode.UseAppPrefixFilters,

            AllowCreateAndDeleteEndpoints = userRoleParameters.Contains("_*") || userRoleParameters.Contains(Const.CreateEndpointRoleParameter),
            ALlowCreateAndDeleteQueries = userRoleParameters.Contains("_*") || userRoleParameters.Contains(Const.CreateQueryRoleParameter),
            AllowCreateAndDeleteViews = userRoleParameters.Contains("_*") || userRoleParameters.Contains(Const.CreateViewRoleParameter)
        });
    }

    #region Connect

    public IActionResult Connect(string id, string userDisplayName, string accessToken)
    {
        return RedirectToAction("Index", new
        {
            dl_token = _crypto.ToSessionString(
                _crypto.DecryptTextDefault(id),
                userDisplayName,
                accessToken)
        });
    }

    public IActionResult Logout()
    {
        if (!String.IsNullOrEmpty(_dataLinqCode.LogoutUrl))
        {
            return Redirect(_dataLinqCode.LogoutUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    #endregion

    #region Start Page

    public IActionResult Start()
    {
        return View();
    }

    #endregion

    #region Api

    async public Task<IActionResult> GetEndPointPrefixes()
    {
        return base.JsonObject(_client == null ?
            null :
            await _client.GetEndPointPrefixes());
    }

    async public Task<IActionResult> GetEndPoints(string filters = null)
    {
        return base.JsonObject(_client == null ?
            null :
            await _client.GetEndPoints(filters?.Split(',')));
    }

    async public Task<IActionResult> GetQueries(string endPoint)
    {
        return base.JsonObject(_client == null ?
            null :
            await _client.GetEndPointQueries(endPoint));
    }

    async public Task<IActionResult> GetViews(string endPoint, string query)
    {
        return base.JsonObject(_client == null ?
            null :
            await _client.GetEndPointQueryViews(endPoint, query));
    }

    async public Task<IActionResult> GetMonacoSnippit()
    {
        return base.JsonObject(_client == null ?
            null :
            await _client.GetMonacoSnippit());
    }

    #endregion

    #region Edit 

    [HttpGet]
    async public Task<IActionResult> EditEndPoint(string endPoint)
    {
        var model = _client == null ?
            null :
            await _client.GetEndPoint(endPoint);

        ViewData["EndPointTypes"] = await _client.GetEndPointTypes();
        ViewData["AccessTree"] = await _accessTree.GetTree(endPoint);
#if DEBUG
        //ViewData["AccessTree"] = ViewData["AccessTree"] ?? DataLinq.Core.Models.AccessTree.Tree.CreateDummy();
#endif
        return View(model);
    }

    [HttpPost]
    async public Task<IActionResult> EditEndPoint(DataLinqEndPoint endPoint)
    {
        try
        {
            if (_client == null)
            {
                throw new Exception("datalinq endpoint no set");
            }

            await Request.Form.AddAuthProperties(endPoint, _accessTree);

            return base.JsonObject(new SuccessModel(await _client.StoreEndPoint(endPoint)));
        }
        catch (Exception ex)
        {
            return base.JsonObject(new SuccessModel(ex));
        }
    }

    [HttpGet]
    async public Task<IActionResult> EditEndPointCss(string endPoint)
    {
        var model = new EndPointCssModel()
        {
            EndPointId = endPoint,
            Css = _client != null ? await _client.GetEndPointCss(endPoint) : null
        };

        return View(model);
    }

    [HttpPost]
    async public Task<IActionResult> EditEndPointCss(EndPointCssModel model)
    {
        try
        {
            if (_client == null)
            {
                throw new Exception("datalinq endpoint no set");
            }

            return base.JsonObject(new SuccessModel(await _client.StoreEndPointCss(model.EndPointId, model.Css)));
        }
        catch (Exception ex)
        {
            return base.JsonObject(new SuccessModel(ex));
        }
    }

    [HttpGet]
    async public Task<IActionResult> EditEndPointJavascript(string endPoint)
    {
        var model = new EndPointJavascriptModel()
        {
            EndPointId = endPoint,
            Javascript = _client != null ? await _client.GetEndPointJavascript(endPoint) : null
        };

        return View(model);
    }

    [HttpPost]
    async public Task<IActionResult> EditEndPointJavascript(EndPointJavascriptModel model)
    {
        try
        {
            if (_client == null)
            {
                throw new Exception("datalinq endpoint no set");
            }

            return base.JsonObject(new SuccessModel(await _client.StoreEndPointJavascript(model.EndPointId, model.Javascript)));
        }
        catch (Exception ex)
        {
            return base.JsonObject(new SuccessModel(ex));
        }
    }

    [HttpGet]
    async public Task<IActionResult> EditEndPointQuery(string endPoint, string query)
    {
        if (_client == null)
        {
            throw new Exception("datalinq endpoint no set");
        }

        var model = await _client.GetEndPointQuery(endPoint, query);

        ViewData["AccessTree"] = await _accessTree.GetTree($"{endPoint}@{query}");
#if DEBUG
        //ViewData["AccessTree"] = ViewData["AccessTree"] ?? DataLinq.Core.Models.AccessTree.Tree.CreateDummy();
#endif

        return View(model);
    }

    [HttpPost]
    async public Task<IActionResult> EditEndPointQuery(DataLinqEndPointQuery query)
    {
        try
        {
            if (_client == null)
            {
                throw new Exception("datalinq endpoint no set");
            }

            await Request.Form.AddAuthProperties(query, _accessTree);

            return base.JsonObject(new SuccessModel(await _client.StoreEndPointQuery(query)));
        }
        catch (Exception ex)
        {
            return base.JsonObject(new SuccessModel(ex));
        }
    }

    [HttpGet]
    async public Task<IActionResult> EditEndPointQueryView(string endPoint, string query, string view)
    {
        if (_client == null)
        {
            throw new Exception("datalinq endpoint no set");
        }

        var model = await _client.GetEndPointQueryView(endPoint, query, view);
        if (model != null && model.IncludedJsLibraries == null)
        {
            model.IncludedJsLibraries = JsLibrary.LegacyDefaultNames; // default value for older versions
        }
        return View(model);
    }

    [HttpPost]
    async public Task<IActionResult> EditEndPointQueryView(DataLinqEndPointQueryView view, bool verifyOnly = false)
    {
        try
        {
            view.IncludedJsLibraries = "";
            var jsLibraries = await _client.GetJsLibraries();

            foreach (var jsLibrary in jsLibraries)
            {
                if (Request.Form["JsLibrary." + jsLibrary.Name].Contains("true"))
                {
                    view.IncludedJsLibraries += (view.IncludedJsLibraries.Length > 0 ? "," : "") + jsLibrary.Name;
                }
            }

            return base.JsonObject(new SuccessModel(await _client.StoreEndPointQueryView(view, verifyOnly)));
        }
        catch (RazorCompileException razorEx)
        {
            return base.JsonObject(new SuccessModel(false)
            {
                ErrorMessage = razorEx.Message,
                CompilerErrors = razorEx.CompilerErrors
            });
        }
        catch (Exception ex)
        {
            return base.JsonObject(new SuccessModel(ex));
        }
    }

    #endregion

    #region Create/New

    async public Task<IActionResult> CreateEndPoint(string endPoint)
    {
        try
        {
            if (_client == null)
            {
                throw new Exception("datalinq endpoint no set");
            }

            return base.JsonObject(await _client.CreateEndPoint(endPoint));
        }
        catch (Exception ex)
        {
            return base.JsonObject(new SuccessModel(ex));
        }
    }

    async public Task<IActionResult> CreateEndPointQuery(string endPoint, string query)
    {
        try
        {
            if (_client == null)
            {
                throw new Exception("datalinq endpoint no set");
            }

            return base.JsonObject(await _client.CreateEndPointQuery(endPoint, query));
        }
        catch (Exception ex)
        {
            return base.JsonObject(new SuccessModel(ex));
        }
    }

    async public Task<IActionResult> CreateEndPointQueryView(string endPoint, string query, string view)
    {
        try
        {
            if (_client == null)
            {
                throw new Exception("datalinq endpoint no set");
            }

            return base.JsonObject(await _client.CreateEndPointQueryView(endPoint, query, view));
        }
        catch (Exception ex)
        {
            return base.JsonObject(new SuccessModel(ex));
        }
    }

    #endregion

    #region Delete

    async public Task<IActionResult> DeleteEndPoint(string endPoint)
    {
        try
        {
            if (_client == null)
            {
                throw new Exception("datalinq endpoint no set");
            }

            if (!await _accessTree.DeleteTree(endPoint))
            {
                throw new Exception("Can't delete access tree for this endpoint");
            }

            return base.JsonObject(new SuccessModel(await _client.DeleteEndPoint(endPoint)));
        }
        catch (Exception ex)
        {
            return base.JsonObject(new SuccessModel(ex));
        }
    }

    async public Task<IActionResult> DeleteEndPointQuery(string endPoint, string query)
    {
        try
        {
            if (_client == null)
            {
                throw new Exception("datalinq endpoint no set");
            }

            if (!await _accessTree.DeleteTree($"{endPoint}@{query}"))
            {
                throw new Exception("Can't delete access tree for this endpoint query");
            }

            return base.JsonObject(new SuccessModel(await _client.DeleteEndPointQuery(endPoint, query)));
        }
        catch (Exception ex)
        {
            return base.JsonObject(new SuccessModel(ex));
        }
    }

    async public Task<IActionResult> DeleteEndPointQueryView(string endPoint, string query, string view)
    {
        try
        {
            if (_client == null)
            {
                throw new Exception("datalinq endpoint no set");
            }

            return base.JsonObject(new SuccessModel(await _client.DeleteEndPointQueryView(endPoint, query, view)));
        }
        catch (Exception ex)
        {
            return base.JsonObject(new SuccessModel(ex));
        }
    }

    #endregion

    #region Verify

    async public Task<IActionResult> VerifyEndPointQueryView(string endPoint, string query, string view)
    {
        try
        {
            return base.JsonObject(new SuccessModel(await _client.VerfifyEndPointQueryView(endPoint, query, view)));
        }
        catch (RazorCompileException razorEx)
        {
            return base.JsonObject(new SuccessModel(false)
            {
                ErrorMessage = razorEx.Message,
                CompilerErrors = razorEx.CompilerErrors
            });
        }
        catch (Exception ex)
        {
            return base.JsonObject(new SuccessModel(ex));
        }
    }

    async public Task<IActionResult> DocInfo(string endPoint, string query, string view, bool rewrite = true)
    {
        try
        {
            await Task.Delay(10);
            if (!String.IsNullOrEmpty(view))
            {
                var dataLinqView = await _client.GetEndPointQueryView(endPoint, query, view);

                //
                // rewriting views not necessary:
                // there or no secured propertiertis in views until now
                //

                //if (dataLinqView != null && rewrite)
                //{
                //    if (await _client.StoreEndPointQueryView(dataLinqView) == false)
                //    {
                //        return base.JsonObject(new SuccessModel(false));
                //    }

                //}

                return base.JsonObject(new SuccessModel(dataLinqView != null));
            }

            if (!String.IsNullOrEmpty(query))
            {
                var dataLinqQuery = await _client.GetEndPointQuery(endPoint, query);
                if (dataLinqQuery != null && rewrite)
                {
                    if (await _client.StoreEndPointQuery(dataLinqQuery) == false)
                    {
                        return base.JsonObject(new SuccessModel(false));
                    }
                }

                return base.JsonObject(new SuccessModel(dataLinqQuery != null));
            }

            var dataLinqEndpoint = await _client.GetEndPoint(endPoint);
            if (dataLinqEndpoint != null && rewrite)
            {
                if (await _client.StoreEndPoint(dataLinqEndpoint) == false)
                {
                    return base.JsonObject(new SuccessModel(false));
                }
            }

            return base.JsonObject(new SuccessModel(dataLinqEndpoint != null));
        }
        catch (Exception ex)
        {
            return base.JsonObject(new SuccessModel(ex));
        }
    }

    #endregion

    #region Auth Info

    async public Task<IActionResult> AuthPrefixes()
    {
        if (_client == null)
        {
            throw new Exception("datalinq endpoint no set");
        }

        return base.JsonObject(await _client.GetAuthPrefixes());
    }

    async public Task<IActionResult> AuthAutocomplete(string prefix, string term)
    {
        if (_client == null)
        {
            throw new Exception("datalinq endpoint no set");
        }

        return base.JsonObject(await _client.GetAuthAutocomplete(prefix, term));
    }

    #endregion
}
