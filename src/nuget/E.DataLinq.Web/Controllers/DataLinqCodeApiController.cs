using E.DataLinq.Core;
using E.DataLinq.Core.Exceptions;
using E.DataLinq.Core.Extensions;
using E.DataLinq.Core.Models;
using E.DataLinq.Core.Models.Abstraction;
using E.DataLinq.Core.Models.Authentication;
using E.DataLinq.Core.Services.Abstraction;
using E.DataLinq.Core.Services.KeyValueStore;
using E.DataLinq.Core.Services.KeyValueStore.Abstraction;
using E.DataLinq.Core.Services.Persistance.Abstraction;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Reflection;
using E.DataLinq.Web.Services;
using E.DataLinq.Web.Services.Abstraction;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    private readonly SemanticKernelService _semanticKernelService;
    private readonly IGitService? _gitService;
    private readonly FeaturesService _featuresService;
    private readonly IKeyValueStoreService _keyValueStore;

    public DataLinqCodeApiController(ILogger<DataLinqCodeApiController> logger,
                                     IPersistanceProviderService persistanceProvider,
                                     DataLinqCompilerService compiler,
                                     DataLinqEndpointTypeService endpointTypes,
                                     IDataLinqCodeIdentityService _identitySerice,
                                     IMonacoSnippetService monacoSnippetService,
                                     JsLibrariesService jsLibraries,
                                     FeaturesService featuresService,
                                     IKeyValueStoreService keyValueStore = null,
                                     IGitService gitService = null,
                                     SemanticKernelService semanticKernelService = null,
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
        _semanticKernelService = semanticKernelService;
        _gitService = gitService;
        _featuresService = featuresService;
        _keyValueStore = keyValueStore;
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

    [HttpGet]
    [Route("getFolderStructure")]
    async public Task<string> GetFolderStructure()
    {
        if (!_identity.HasDataLinqCodeRole())
            throw new Exception("Not authorized");

        return await _persistanceProvider.GetFolderStructure();
    }

    [HttpGet]
    [Route("capabilities/features")]
    public IActionResult GetFeatures()
    {
        if (!_identity.HasDataLinqCodeRole())
            throw new Exception("Not authorized");

        return base.JsonObject(_featuresService.GetFeatures());
    }

    #endregion

    #region KeyValueStore (Secrets & Constants)

    [HttpGet]
    [Route("keyvaluestore/{store}/keys")]
    public IActionResult KeyValueStoreKeys(string store)
    {
        if (!_identity.HasDataLinqCodeRole())
            throw new Exception("Not authorized");

        if (_keyValueStore is null)
            throw new Exception("KeyValueStore is not configured");

        var storeType = ParseStoreType(store);

        // Never return decrypted secret values here, only the keys.
        return base.JsonObject(new { keys = _keyValueStore.GetKeys(storeType) });
    }

    [HttpGet]
    [Route("keyvaluestore/{store}/value")]
    public IActionResult KeyValueStoreValue(string store, string key)
    {
        if (!_identity.HasDataLinqCodeRole())
            throw new Exception("Not authorized");

        if (_keyValueStore is null)
            throw new Exception("KeyValueStore is not configured");

        var storeType = ParseStoreType(store);

        // Only expose plain text values (constants) through this endpoint.
        if (storeType == KeyValueStoreType.Secret)
            throw new Exception("Secret values can not be read through this endpoint");

        return base.JsonObject(new { key, value = _keyValueStore.GetValue(storeType, key) });
    }

    [HttpPost]
    [Route("keyvaluestore/{store}/set")]
    public IActionResult KeyValueStoreSet(string store, [FromForm] string key, [FromForm] string value)
    {
        if (!_identity.HasDataLinqCodeRole())
            throw new Exception("Not authorized");

        if (_keyValueStore is null)
            throw new Exception("KeyValueStore is not configured");

        if (String.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key must not be empty");

        var storeType = ParseStoreType(store);

        _keyValueStore.SetValue(storeType, key, value ?? "");

        return base.JsonObject(new { success = true });
    }

    [HttpPost]
    [Route("keyvaluestore/{store}/delete")]
    public IActionResult KeyValueStoreDelete(string store, [FromForm] string key)
    {
        if (!_identity.HasDataLinqCodeRole())
            throw new Exception("Not authorized");

        if (_keyValueStore is null)
            throw new Exception("KeyValueStore is not configured");

        var storeType = ParseStoreType(store);

        return base.JsonObject(new { success = _keyValueStore.DeleteKey(storeType, key) });
    }

    private static KeyValueStoreType ParseStoreType(string store)
        => store?.ToLowerInvariant() switch
        {
            "secret" or "secrets" => KeyValueStoreType.Secret,
            "constant" or "constants" => KeyValueStoreType.Constant,
            _ => throw new ArgumentException($"Unknown key value store '{store}'")
        };

    #endregion

    #region Edit (Post)

    [HttpPost]
    [Route("askdatalinqcopilot")]
    public async Task<string> AskDataLinqCopilot()
    {
        if (_semanticKernelService == null)
        {
            return "AI services are not configured";
        }

        var requestPayload = await Request.FromBody<AskDataLinqCopilotRequest>();

        return await SecureMethodHandler(async () =>
            await _semanticKernelService.ProcessAsync(requestPayload.Questions));
    }

    [HttpPost]
    [Route("post/saveFolderStructure")]
    async public Task<IActionResult> SaveFolderStructure([FromBody] Dictionary<string, List<string>> folderStructure)
    {
        return await SecureMethodHandler(async () =>
        {
            if (folderStructure == null || !folderStructure.Any())
            {
                throw new ArgumentException("Folder structure missing");
            }

            return base.JsonObject(new SuccessModel(await _persistanceProvider.SaveFolderStructure(folderStructure)).OnSuccess((model) =>
            {

            }));
        });
    }

    [HttpPost]
    [Route("post/commitAndPushChanges")]
    public async Task<GitCommitChangesResult> CommitAndPushChanges([FromBody] GitCommitChangesRequest details)
    {
        return await SecureMethodHandler(async () =>
        {
            if (!_gitService.IsEnabled)
                return new GitCommitChangesResult { Error = "Version Control is not configured" };

            var parts = details.Id.Split('@');

            var code = parts.Length switch
            {
                2 => (await _persistanceProvider.GetEndPointQuery(parts[0], parts[1])).Statement,
                3 => (await _persistanceProvider.GetEndPointQueryView(parts[0], parts[1], parts[2])).Code,
                _ => null
            };

            if (code is null)
                code = "";

            if (!await _persistanceProvider.StoreCode(details.Id, code))
                return new GitCommitChangesResult { Error = "Code could not be saved!" };

            if (!await _persistanceProvider.UpdateGitStatus(details.Id, true))
                return new GitCommitChangesResult { Error = "Git status change failed!" };

            var gitAction = await _gitService.CommitAndPushAsync(details.Message, details.Name);
            if (!gitAction.Success)
            {
                await _persistanceProvider.UpdateGitStatus(details.Id, false);
                return new GitCommitChangesResult { Error = gitAction.Error.Details };
            }

            return new GitCommitChangesResult { Success = true, Message = gitAction.Message };
        });
    }

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
            query.Changed = DateTime.UtcNow;

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
                Created = DateTime.UtcNow,
                Changed = DateTime.Now
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
            if (_gitService.IsEnabled)
            {
                await _persistanceProvider.DeleteCode(endPointId);
                await _gitService.CommitAndPushAsync($"Endpoint deleted: {endPointId}",_identity.Name);
            }

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
            if (_gitService.IsEnabled)
            {
                await _persistanceProvider.DeleteCode($"{endPointId}@{queryId}");
                await _gitService.CommitAndPushAsync($"Query deleted: {endPointId}@{queryId}", _identity.Name);
            }

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
            if (_gitService.IsEnabled)
            {
                await _persistanceProvider.DeleteCode($"{endPointId}@{queryId}@{viewId}");
                await _gitService.CommitAndPushAsync($"View deleted: {endPointId}@{queryId}@{viewId}", _identity.Name);
            }

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
    [Route("checkGitStatus/{endPointId}/{queryId}/{viewId}")]
    public async Task<GitCommitChangesResult> CheckGitStatus(string endPointId, string queryId, string viewId)
    {
        return await SecureMethodHandler(async () =>
        {
            if (!_gitService.IsEnabled)
                return new GitCommitChangesResult() { Error = "Version Control is not configured" };

            bool isQuery = viewId.Equals("_isQuery");
            dynamic entity = isQuery
                ? await _persistanceProvider.GetEndPointQuery(endPointId, queryId)
                : await _persistanceProvider.GetEndPointQueryView(endPointId, queryId, viewId);

            string entityType = isQuery ? "Query" : "View";

            if (entity == null || entity.Changed == null)
                return new GitCommitChangesResult { Error = $"{entityType} or changed date is null" };

            if (!entity.Changed.Equals(entity.ChangedGit))
                return new GitCommitChangesResult { Error = "Changed date and Git date dont match" };

            return new GitCommitChangesResult
            {
                Success = true,
                Message = $"{entityType} is up to date"
            };
        });
    }

    [HttpGet]
    [Route("initializeGitRepository")]
    public async Task<GitCommitChangesResult> InitializeGitRepository()
    {
        return await SecureMethodHandler(async () =>
        {
            if (!_gitService.IsEnabled)
                return new GitCommitChangesResult { Error = "Version Control is not configured" };

            if (!await _persistanceProvider.DeleteLocalGitFolder())
                return new GitCommitChangesResult { Error = "_gitFolder not found" };

            var endpoints = await _persistanceProvider.GetEndPointIds(null);

            var allData = (await Task.WhenAll(endpoints.Select(async endpointId =>
            {
                var queryIds = await _persistanceProvider.GetQueryIds(endpointId);
                return await Task.WhenAll(queryIds.Select(async queryId => new
                {
                    EndpointId = endpointId,
                    QueryId = queryId,
                    Views = await _persistanceProvider.GetViewIds(endpointId, queryId)
                }));
            }))).SelectMany(x => x).ToList();

            var files = allData
                .Select(x => $"{x.EndpointId}@{x.QueryId}")
                .Distinct()
                .Concat(allData.SelectMany(x => x.Views.Select(v => $"{x.EndpointId}@{x.QueryId}@{v}")))
                .ToList();

            foreach (var file in files)
            {
                var parts = file.Split('@');

                if (parts.Length is not 2 and not 3)
                    continue;

                var code = parts.Length switch
                {
                    2 => (await _persistanceProvider.GetEndPointQuery(parts[0], parts[1])).Statement,
                    _ => (await _persistanceProvider.GetEndPointQueryView(parts[0], parts[1], parts[2])).Code
                };

                if (code is null)
                    code = "";

                if (!await _persistanceProvider.StoreCode(file, code))
                    return new GitCommitChangesResult { Error = "Code could not be saved!" };

                if (!await _persistanceProvider.UpdateGitStatus(file, true))
                    return new GitCommitChangesResult { Error = "Git status change failed!" };
            }

            var gitAction = await _gitService.CommitAndPushAsync("Init DataLinq", "DataLinq Bot");
            if (!gitAction.Success)
            {
                await Task.WhenAll(files.Select(f => _persistanceProvider.UpdateGitStatus(f, false)));
                return new GitCommitChangesResult { Error = gitAction.Error.Details };
            }

            return new GitCommitChangesResult { Success = true, Message = "Completed initializing push" };
        });
    }

    [HttpGet]
    [Route("capabilities/jslibs")]
    public IActionResult GetJsLibraries() => base.JsonObject(_jsLibraries.Libraries);

    [HttpGet]
    [Route("monacosnippit")]
    public IActionResult GetSnippets([FromQuery] string lang, [FromQuery] string helper = "dlh")
    {
        string json = _monacoSnippetService.BuildSnippetJson(lang, helper);
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
