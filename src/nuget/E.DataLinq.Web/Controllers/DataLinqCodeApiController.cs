using E.DataLinq.Core;
using E.DataLinq.Core.Exceptions;
using E.DataLinq.Core.Extensions;
using E.DataLinq.Core.Models;
using E.DataLinq.Core.Models.Authentication;
using E.DataLinq.Core.Services.Abstraction;
using E.DataLinq.Core.Services.Persistance.Abstraction;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Reflection;
using E.DataLinq.Web.Services;
using E.DataLinq.Web.Services.Abstraction;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Controllers;

[Route("[controller]")]
[ApiController]
[HostAuthentication(HostAuthenticationTypes.DataLinqAccessToken)]
public class DataLinqCodeApiController : ApiBaseController
{
    private readonly ILogger<DataLinqCodeApiController> _logger;
    private readonly IPersistanceProviderService _persistanceProvider;
    private readonly IHostAuthenticationService _hostAuthentication;
    private readonly DataLinqCompilerService _compiler;
    private readonly DataLinqCodeIdentity _identity;
    private readonly IMonacoSnippetService _monacoSnippetService;
    private readonly DataLinqEndpointTypeService _endpointTypes;
    private readonly JsLibrariesService _jsLibraries;
    private readonly IDataLinqApiNotificationService _notification;

    public DataLinqCodeApiController(ILogger<DataLinqCodeApiController> logger,
                                     IPersistanceProviderService persistanceProvider,
                                     DataLinqCompilerService compiler,
                                     DataLinqEndpointTypeService endpointTypes,
                                     IDataLinqCodeIdentityService _identitySerice,
                                     IMonacoSnippetService monacoSnippetService,
                                     JsLibrariesService jsLibraries,
                                     IHostAuthenticationService hostAuthentication = null,
                                     IDataLinqApiNotificationService notification = null)
    {
        _logger = logger;
        _persistanceProvider = persistanceProvider;
        _compiler = compiler;
        _endpointTypes = endpointTypes;
        _identity = _identitySerice.CurrentIdentity();
        _monacoSnippetService = monacoSnippetService;
        _jsLibraries = jsLibraries;
        _hostAuthentication = hostAuthentication;
        _notification = notification;
    }

    #region Get

    [HttpGet]
    [Route("get/{endPointId}")]
    async public Task<IActionResult> EndPoint(string endPointId)
    {
        return await SecureMethodHandler(async () =>
        {
            var endPoint = await _persistanceProvider.GetEndPoint(endPointId);
            return base.JsonObject(endPoint);
        }, new[] { endPointId });
    }

    [HttpGet]
    [Route("get/{endPointId}/{queryId}")]
    async public Task<IActionResult> EndPointQuery(string endPointId, string queryId)
    {
        return await SecureMethodHandler(async () =>
        {
            var query = await _persistanceProvider.GetEndPointQuery(endPointId, queryId);
            return base.JsonObject(query);
        }, new[] { endPointId });
    }

    [HttpGet]
    [Route("get/{endPointId}/{queryId}/{viewId}")]
    async public Task<IActionResult> EndPointQueryView(string endPointId, string queryId, string viewId)
    {
        return await SecureMethodHandler(async () =>
        {
            var view = await _persistanceProvider.GetEndPointQueryView(endPointId, queryId, viewId);
            return base.JsonObject(view);
        }, new[] { endPointId });
    }

    [HttpGet]
    [Route("css/{endPointId}")]
    async public Task<string> EndPointCss(string endPointId)
    {
        return await SecureMethodHandler(async () =>
        {
            var css = await _persistanceProvider.GetEndPointCss(endPointId);
            return css;
        }, new[] { endPointId });
    }

    [HttpGet]
    [Route("css/view/{id}")]
    async public Task<string> ViewCss(string id)
    {
        return await SecureMethodHandler(async () =>
        {
            var css = await _persistanceProvider.GetViewCss(id);
            return css;
        }, new[] { id });
    }

    [HttpGet]
    [Route("js/{endPointId}")]
    async public Task<string> EndPointJavascript(string endPointId)
    {
        return await SecureMethodHandler(async () =>
        {
            var js = await _persistanceProvider.GetEndPointJavascript(endPointId);
            return js;
        }, new[] { endPointId });
    }

    [HttpGet]
    [Route("js/view/{id}")]
    async public Task<string> ViewJs(string id)
    {
        return await SecureMethodHandler(async () =>
        {
            var js = await _persistanceProvider.GetViewJs(id);
            return js;
        }, new[] { id });
    }

    [HttpGet]
    [Route("types/endpoint")]
    public IDictionary<int, string> EndPointTypes()
    {
        if (!_identity.HasDataLinqCodeRole())
        {
            throw new Exception("Not authorized");
        }

        return _endpointTypes.TypeDictionary;
    }

    [HttpGet]
    [Route("endpointprefixes")]
    async public Task<IDictionary<string, IEnumerable<string>>> EndPointPrefixes()
    {
        return await SecureMethodHandler(async () =>
            await _persistanceProvider.GetEndPointPrefixes());
    }

    [HttpGet]
    [Route("endpoints")]
    async public Task<IEnumerable<string>> EndPoints(string filters = "")
    {
        return await SecureMethodHandler(async () =>
        {
            return (await _persistanceProvider
                .GetEndPointIds(filters?.Split(',')))
                .Where(e => _identity.HasEndPointRoleParameter(e) ||
                            _persistanceProvider.EndPointCreator(e).Result.Equals(_identity?.Name, StringComparison.OrdinalIgnoreCase));
        });
    }

    [HttpGet]
    [Route("{endPointId}/queries")]
    async public Task<IEnumerable<string>> EndPointQueries(string endPointId)
    {
        return await SecureMethodHandler(async () =>
        {
            return await _persistanceProvider.GetQueryIds(endPointId);
        }, new[] { endPointId });
    }

    [HttpGet]
    [Route("{endPointId}/{queryId}/views")]
    async public Task<IEnumerable<string>> EndPointQueryViews(string endPointId, string queryId)
    {
        return await SecureMethodHandler(async () =>
        {
            return await _persistanceProvider.GetViewIds(endPointId, queryId);
        }, new[] { endPointId });
    }

    #endregion

    #region Edit (Post)

    [HttpPost]
    [Route("post/endpoint")]
    async public Task<IActionResult> StoreEndPoint()
    {
        var endPoint = await Request.FromBody<DataLinqEndPoint>();

        return await SecureMethodHandler(async () =>
        {
            return base.JsonObject(new SuccessModel(await _persistanceProvider.StoreEndPoint(endPoint)).OnSuccess((model) =>
            {
                _notification?.ItemUpdated(endPoint.Id);
            }));
        }, new[] { endPoint.Id });
    }

    [HttpPost]
    [Route("post/endpointcss")]
    async public Task<IActionResult> StoreEndPointCss([FromForm] string endPointId, [FromForm] string css)
    {
        return await SecureMethodHandler(async () =>
        {
            if (String.IsNullOrEmpty(endPointId))
            {
                throw new ArgumentException("Invalid endPointId");
            }

            if (await _persistanceProvider.GetEndPoint(endPointId) == null)
            {
                throw new ArgumentException($"Unknown endPoint Id {endPointId}");
            }

            return base.JsonObject(new SuccessModel(await _persistanceProvider.StoreEndPointCss(endPointId, css)).OnSuccess((model) =>
            {

            }));
        }, new[] { endPointId });
    }

    [HttpPost]
    [Route("post/viewcss")]
    async public Task<IActionResult> StoreViewCss([FromForm] string id, [FromForm] string css)
    {
        return await SecureMethodHandler(async () =>
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Invalid endPointId");
            }

            var endpointId = id.Split('@')[0];

            if (await _persistanceProvider.GetEndPoint(endpointId) == null)
            {
                throw new ArgumentException($"Unknown endPoint Id {id}");
            }

            return base.JsonObject(new SuccessModel(await _persistanceProvider.StoreViewCss(id, css)).OnSuccess((model) =>
            {

            }));
        }, new[] { id });
    }

    [HttpPost]
    [Route("post/endpointjs")]
    async public Task<IActionResult> StoreEndPointJavascript([FromForm] string endPointId, [FromForm] string js)
    {
        return await SecureMethodHandler(async () =>
        {
            if (String.IsNullOrEmpty(endPointId))
            {
                throw new ArgumentException("Invalid endPointId");
            }

            if (await _persistanceProvider.GetEndPoint(endPointId) == null)
            {
                throw new ArgumentException($"Unknown endPoint Id {endPointId}");
            }

            return base.JsonObject(new SuccessModel(await _persistanceProvider.StoreEndPointJavascript(endPointId, js)).OnSuccess((model) =>
            {

            }));
        }, new[] { endPointId });
    }

    [HttpPost]
    [Route("post/viewjs")]
    async public Task<IActionResult> StoreViewJs([FromForm] string id, [FromForm] string js)
    {
        return await SecureMethodHandler(async () =>
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Invalid endPointId");
            }

            if (await _persistanceProvider.GetEndPoint(id.Split('@')[0]) == null)
            {
                throw new ArgumentException($"Unknown endPoint Id {id}");
            }

            return base.JsonObject(new SuccessModel(await _persistanceProvider.StoreViewJs(id, js)).OnSuccess((model) =>
            {

            }));
        }, new[] { id });
    }

    [HttpPost]
    [Route("post/{endpointId}/query")]
    async public Task<IActionResult> StoreEndPointQuery(string endPointId)
    {
        return await SecureMethodHandler(async () =>
        {
            var query = await Request.FromBody<DataLinqEndPointQuery>();

            query.EndPointId = endPointId;

            return base.JsonObject(new SuccessModel(await _persistanceProvider.StoreEndPointQuery(query)).OnSuccess((model) =>
            {
                _notification?.ItemUpdated($"{endPointId}@{query.QueryId}");
            }));
        }, new[] { endPointId });
    }

    [HttpPost]
    [Route("post/{endPointId}/{queryId}/view")]
    async public Task<IActionResult> StoreEndPointQueryView(string endPointId, string queryId, bool verifyOnly = false)
    {
        return await SecureMethodHandler(async () =>
        {
            var view = await Request.FromBody<DataLinqEndPointQueryView>();

            view.EndPointId = endPointId;
            view.QueryId = queryId;
            view.Changed = DateTime.UtcNow;

            await _compiler.ValidateRazorCode(view);

            if (verifyOnly == true)
            {
                return base.JsonObject(new SuccessModel());
            }

            return base.JsonObject(new SuccessModel(await _persistanceProvider.StoreEndPointQueryView(view)).OnSuccess((model) =>
            {
                if (!verifyOnly)
                {
                    _notification?.ItemUpdated($"{endPointId}@{queryId}@{view.ViewId}");
                }
            }));
        }, new[] { endPointId });
    }

    #endregion

    #region Create/New

    [HttpGet]
    [Route("create/{endPointId}")]
    async public Task<IActionResult> CreateEndPoint(string endPointId)
    {
        return await SecureMethodHandler(async () =>
        {
            endPointId = endPointId.ToValidDataLinqRouteId();

            if (await _persistanceProvider.GetEndPoint(endPointId) != null)
            {
                throw new Exception($"Endpoint {endPointId} alread exists");
            }

            var endPoint = new DataLinqEndPoint()
            {
                Id = endPointId,
                Subscriber = this.User.GetUsername(),
                SubscriberId = this.User.GetUserId(),
                Access = new[] { this.User.GetUsername() },
                Created = DateTime.UtcNow
            };

            return base.JsonObject(new SuccessCreatedModel(await _persistanceProvider.StoreEndPoint(endPoint))
            {
                EndPointId = endPointId
            }
            .OnSuccess((model) =>
            {
                _notification?.ItemCreated(endPointId);
            }));
        }, new[] { Const.CreateEndpointRoleParameter });
    }

    [HttpGet]
    [Route("create/{endPointId}/{queryId}")]
    async public Task<IActionResult> CreateEndPointQuery(string endPointId, string queryId)
    {
        return await SecureMethodHandler(async () =>
        {
            queryId = queryId.ToValidDataLinqRouteId();

            if (await _persistanceProvider.GetEndPoint(endPointId) == null)
            {
                throw new Exception($"Endpoint {endPointId} not exists");
            }

            if (await _persistanceProvider.GetEndPointQuery(endPointId, queryId) != null)
            {
                throw new Exception($"Query {endPointId}@{queryId} allready exists");
            }

            var query = new DataLinqEndPointQuery()
            {
                EndPointId = endPointId,
                QueryId = queryId,
                Access = new[] { this.User.GetUsername() },
                Created = DateTime.UtcNow
            };

            return base.JsonObject(new SuccessCreatedModel(await _persistanceProvider.StoreEndPointQuery(query))
            {
                EndPointId = endPointId,
                QueryId = queryId,
            }
            .OnSuccess((model) =>
            {
                _notification?.ItemCreated($"{endPointId}@{queryId}");
            }));
        }, new[] { endPointId, Const.CreateQueryRoleParameter });
    }

    [HttpGet]
    [Route("create/{endPointId}/{queryId}/{viewId}")]
    async public Task<IActionResult> CreateEndPointQueryView(string endPointId, string queryId, string viewId)
    {
        return await SecureMethodHandler(async () =>
        {
            viewId = viewId.ToValidDataLinqRouteId();

            if (await _persistanceProvider.GetEndPoint(endPointId) == null)
            {
                throw new Exception($"Endpoint {endPointId} not exists");
            }

            if (await _persistanceProvider.GetEndPointQuery(endPointId, queryId) == null)
            {
                throw new Exception($"Query {endPointId}@{queryId} not exists");
            }

            if (await _persistanceProvider.GetEndPointQueryView(endPointId, queryId, viewId) != null)
            {
                throw new Exception($"View {endPointId}@{queryId}@{viewId} allready exists");
            }

            var view = new DataLinqEndPointQueryView()
            {
                EndPointId = endPointId,
                QueryId = queryId,
                ViewId = viewId,
                Created = DateTime.UtcNow,
                Changed = DateTime.UtcNow,
                Code = @"@*

Model:
======
Model.Success                       (bool)
Model.CountRecords                  (int)
Model.ElapsedMillisconds            (int)
Model.Records                       (IDictionary<string,object> [])
Model.RecordColumns()               (IEnumerable<string>)
Model.QueryString                   (NameValueCollection)
Model.FilterString                  (string)

Linq:
=====
Model.Records.Where(r=>""value"".Equals(r[""field""])
Model.Records.Where(r=>String.IsNullOrEmpty(Model.QueryString[""x""]) || Model.QueryString[""x""].Equals(r[""x_field""])).OrderBy(r=>r[""data_field"")
...
Model.Records.Where(r=>""value"".Equals(r[""field""]).Sum(r=>Convert.ToDouble(r[""length_field""]))
...

DataLinqHelper (DLH)
====================

The DataLinqHelper is a helper class that provides methods for displaying data as well as for forms, etc.
For more information, see Help (?).
*@

@DLH.Table(Model.Records, max: 100)
"
            };

            return base.JsonObject(new SuccessCreatedModel(await _persistanceProvider.StoreEndPointQueryView(view))
            {
                EndPointId = endPointId,
                QueryId = queryId,
                ViewId = viewId
            }
            .OnSuccess((model) =>
            {
                _notification?.ItemCreated($"{endPointId}@{queryId}@{viewId}");
            }));
        }, new[] { endPointId, Const.CreateViewRoleParameter });
    }

    #endregion

    #region Delete

    [HttpGet]
    [Route("delete/{endPointId}")]
    async public Task<IActionResult> DeleteEndPoint(string endPointId)
    {
        return await SecureMethodHandler(async () =>
        {
            return base.JsonObject(new SuccessModel(await _persistanceProvider.DeleteEndPoint(endPointId)).OnSuccess((action) =>
            {
                _notification?.ItemDeleted(endPointId);
            }));
        }, new[] { endPointId, Const.CreateEndpointRoleParameter });
    }

    [HttpGet]
    [Route("delete/{endPointId}/{queryId}")]
    async public Task<IActionResult> DeleteEndPointQuery(string endPointId, string queryId)
    {
        return await SecureMethodHandler(async () =>
        {
            return base.JsonObject(new SuccessModel(await _persistanceProvider.DeleteEndPointQuery(endPointId, queryId)).OnSuccess((action) =>
            {
                _notification?.ItemDeleted($"{endPointId}@{queryId}");
            }));
        }, new[] { endPointId, Const.CreateQueryRoleParameter });
    }

    [HttpGet]
    [Route("delete/{endPointId}/{queryId}/{viewId}")]
    async public Task<IActionResult> DeleteEndPointQueryView(string endPointId, string queryId, string viewId)
    {
        return await SecureMethodHandler(async () =>
        {
            return base.JsonObject(new SuccessModel(await _persistanceProvider.DeleteEndPointQueryView(endPointId, queryId, viewId)).OnSuccess((action) =>
            {
                _notification?.ItemDeleted($"{endPointId}@{queryId}@{viewId}");
            }));
        }, new[] { endPointId, Const.CreateViewRoleParameter });
    }

    #endregion

    #region Verify

    [HttpGet]
    [Route("verify/{endPointId}/{queryId}/{viewId}")]
    async public Task<IActionResult> VerifyEndPointQueryView(string endPointId, string queryId, string viewId)
    {
        return await SecureMethodHandler(async () =>
        {
            var view = await _persistanceProvider.GetEndPointQueryView(endPointId, queryId, viewId);
            if (view == null)
            {
                throw new ArgumentException($"Unknown view {endPointId}@{queryId}@{viewId}");
            }

            await _compiler.ValidateRazorCode(view);

            return base.JsonObject(new SuccessModel());
        }, new[] { endPointId });
    }

    [HttpGet]
    [Route("capabilities/jslibs")]
    public IActionResult GetJsLibraries() => base.JsonObject(_jsLibraries.Libraries);

    [HttpGet]
    [Route("monacosnippit")]
    public IActionResult GetSnippets([FromQuery] string lang)
    {
        string json = _monacoSnippetService.BuildSnippetJson(lang);
        return Content(json, "application/json");
    }

    #endregion

    #region Auth

    [HttpGet]
    [Route("auth/prefixes")]
    async public Task<IEnumerable<string>> AuthPrefixes()
    {
        return await SecureMethodHandler(async () =>
        {
            if (_hostAuthentication == null)
            {
#if DEBUG
                return new[] { "beatles::", "stones::" };
#else
            return null;
#endif
            }

            return await _hostAuthentication.AuthPrefixesAsync(this.HttpContext);
        });
    }

    [HttpGet]
    [Route("auth/autocomplete")]
    async public Task<IEnumerable<string>> AuthAutocomplete(string prefix, string term)
    {
        return await SecureMethodHandler(async () =>
        {
            if (_hostAuthentication == null)
            {
#if DEBUG
                switch (prefix)
                {
                    case "beatles::":
                        return new[] { "john", "paul", "george", "ringo" };
                    case "stones::":
                        return new[] { "mick", "keith", "charlie", "brian", "bill", "ron" };
                }
#endif
                return null;
            }

            return await _hostAuthentication.AuthAutocompleteAsync(this.HttpContext, prefix, term);
        });
    }

    #endregion

    async private Task<T> SecureMethodHandler<T>(Func<Task<T>> func, string[] requiredEndPointRights = null)
    {
        try
        {
            if (!_identity.HasDataLinqCodeRole())
            {
                throw new Exception("Not authorized");
            }

            if (requiredEndPointRights != null && requiredEndPointRights.Length > 0)
            {
                if (!_identity.HasRoleParameters(requiredEndPointRights))
                {
                    bool isCreator = false;
                    foreach (var endPoint in requiredEndPointRights.Where(r => !r.StartsWith("_")))
                    {
                        if (_identity.Name.Equals(await _persistanceProvider.EndPointCreator(endPoint), StringComparison.OrdinalIgnoreCase))
                        {
                            isCreator = true;
                            break;
                        }
                    }

                    if (!isCreator)
                    {
                        throw new Exception("Not authorized");
                    }
                }
            }

            return await func();
        }
        catch (RazorCompileException razorEx)
        {
            if (typeof(T) == typeof(IActionResult))
            {
                var actionResult = (T)base.JsonObject(new SuccessModel(false)
                {
                    ErrorMessage = "Razor compiler errors",
                    CompilerErrors = razorEx.CompilerErrors
                });

                return actionResult;
            }

            return default(T);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SecureMethodHandler: user={username} roles={userroles}", _identity?.Name, String.Join(",", _identity?.Roles ?? []));

            if (typeof(T) == typeof(IActionResult))
            {
                return (T)base.JsonObject(new SuccessModel(ex));
            }

            return default(T);
        }
    }
}
