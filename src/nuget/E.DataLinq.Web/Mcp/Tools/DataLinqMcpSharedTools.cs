using ModelContextProtocol;
using E.DataLinq.Core;
using E.DataLinq.Core.Models;
using E.DataLinq.Core.Extensions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static E.DataLinq.Web.Mcp.DataLinqMcpToolContext;

namespace E.DataLinq.Web.Mcp;

// Tools used by all DataLinq skills (PDF reports and dashboards).
// Read-only metadata tools need no approval. Everything that runs a query or changes stored items
// is approved by the user via MCP elicitation on the server side (see DataLinqMcpApproval).
[McpServerToolType]
public class DataLinqMcpSharedTools
{
    private readonly DataLinqMcpToolContext _ctx;

    public DataLinqMcpSharedTools(DataLinqMcpToolContext ctx)
    {
        _ctx = ctx;
    }

    internal const string StatementSyntax =
        "Statement syntax: @param (or :param) = bound DB parameter (preferred); {{param}} = raw text replacement (SQL injection risk). " +
        "A @param without a matching URL parameter is not bound -> DB error (e.g. 'Insufficient parameters'); make it optional with #if. " +
        "'#if a,b' ... '#endif' (directive at line start, no spaces around commas): lines kept only if ALL listed parameters exist and are non-empty; " +
        "nestable; no #else, no negation. Optional filter: 'WHERE 1=1' / '#if x' / 'AND col = @x' / '#endif'. " +
        "'_orderby' parameter: '@_orderby' in ORDER BY is replaced by validated fields ('-COL' = DESC). Details: get_query_reference.";

    internal const string FlagPdfReportMode =
        "PDF report mode: view is rendered as paged PDF report (@PDF.* helper, pdf-lib export, Download PDF/Print toolbar). false = normal HTML page/dashboard.";
    internal const string FlagPdfCompatibilityNotice =
        "PDF reports only: show a browser compatibility notice to users (PDF export works best in Chromium browsers).";
    internal const string FlagPdfReportEditing =
        "PDF reports only: enables the visual editor (drag & drop, copy, shapes, form-input tool) when the report is shown framed inside DataLinq Code " +
        "on Test/Development instances. It does NOT make fields editable for end users; end-user fields are .dl-input form inputs placed in the code/editor.";
    internal const string FlagCacheToken =
        "DataLinq cache token settings (lifetime days/hours/minutes and max usage) for token based access to this view. Leave unchanged unless asked.";

    #region Read (metadata, no approval)

    [McpServerTool(Name = "whoami", ReadOnly = true)]
    [Description("Returns the DataLinq user, roles and environment this MCP session is bound to.")]
    public object WhoAmI()
    {
        var identity = _ctx.Identity();
        return new
        {
            user = identity.Name,
            roles = identity.Roles,
            environment = _ctx.Options.EnvironmentType.ToString()
        };
    }

    [McpServerTool(Name = "list_endpoints", ReadOnly = true)]
    [Description("Lists DataLinq endpoint ids the current user may edit. Optional comma separated filters.")]
    async public Task<IEnumerable<string>> ListEndpoints([Description("Optional comma separated filters")] string filters = "")
    {
        var identity = _ctx.Identity();
        var ids = await _ctx.Persistance.GetEndPointIds(String.IsNullOrWhiteSpace(filters) ? null : filters.Split(','));
        var result = new List<string>();

        foreach (var id in ids)
        {
            if (identity.HasEndPointRoleParameter(id) ||
                identity.Name.Equals(await _ctx.Persistance.EndPointCreator(id), StringComparison.OrdinalIgnoreCase))
            {
                result.Add(id);
            }
        }

        return result;
    }

    [McpServerTool(Name = "list_queries", ReadOnly = true)]
    [Description("Lists query ids of an endpoint.")]
    async public Task<IEnumerable<string>> ListQueries(string endpointId)
    {
        await _ctx.Authorize(endpointId);
        return await _ctx.Persistance.GetQueryIds(endpointId);
    }

    [McpServerTool(Name = "list_views", ReadOnly = true)]
    [Description("Lists view ids of a query.")]
    async public Task<IEnumerable<string>> ListViews(string endpointId, string queryId)
    {
        await _ctx.Authorize(endpointId);
        return await _ctx.Persistance.GetViewIds(endpointId, queryId);
    }

    [McpServerTool(Name = "get_query", ReadOnly = true)]
    [Description("Returns the full query definition (statement, access, domains, JSON API headers, test parameters). Access token values are masked; endpoint details are never returned.")]
    async public Task<object> GetQuery(string endpointId, string queryId)
    {
        await _ctx.Authorize(endpointId);
        var query = await _ctx.Persistance.GetEndPointQuery(endpointId, queryId)
            ?? throw new McpException($"Query {endpointId}@{queryId} not found");

        return QueryResult(query);
    }

    [McpServerTool(Name = "get_view", ReadOnly = true)]
    [Description("Returns a view with all properties (Razor code, CSS, JS, PDF flags, cache token settings). Expensive for large views - prefer get_view_outline/get_view_page.")]
    async public Task<object> GetView(string endpointId, string queryId, string viewId)
    {
        await _ctx.Authorize(endpointId);
        var view = await _ctx.GetViewOrThrow(endpointId, queryId, viewId);

        return new
        {
            view = ViewResult(view),
            css = await _ctx.Persistance.GetViewCss(view.Route()),
            js = await _ctx.Persistance.GetViewJs(view.Route())
        };
    }

    [McpServerTool(Name = "get_view_outline", ReadOnly = true)]
    [Description("Returns a cheap outline of a view WITHOUT code. PDF report views (mode 'pdf-pages'): section 0 = code before the first @PDF.NewPage, " +
                 "1..n = pages, optional last = code after the last page. Other views (mode 'html-sections'): top-level HTML/Razor blocks " +
                 "(style, script, @{ } code, loops, <div>/<section> ...). Each section has startLine/endLine and a short summary " +
                 "(ids/classes, DLH calls, referenced routes). Use get_view_page to read one section and patch_view to change it.")]
    async public Task<object> GetViewOutline(string endpointId, string queryId, string viewId)
    {
        await _ctx.Authorize(endpointId);
        var view = await _ctx.GetViewOrThrow(endpointId, queryId, viewId);

        var code = view.Code ?? String.Empty;
        var mode = DataLinqMcpViewSections.DetectMode(code);
        var sections = DataLinqMcpViewSections.Parse(code, mode);

        return new
        {
            route = view.Route(),
            pdfReportMode = view.PDFReportMode,
            mode,
            totalLines = DataLinqMcpViewSections.SplitLines(code).Length,
            totalChars = code.Length,
            sections = sections.Select(s => new
            {
                page = s.Index,
                kind = s.Kind,
                startLine = s.StartLine,
                endLine = s.EndLine,
                chars = s.Length,
                summary = s.Summary
            })
        };
    }

    [McpServerTool(Name = "get_view_page", ReadOnly = true)]
    [Description("Returns the code of ONE section of a view (see get_view_outline). " +
                 "Lines are prefixed with their line number in the full view ('12: ...'); strip the prefix when copying text for patch_view.")]
    async public Task<object> GetViewPage(string endpointId, string queryId, string viewId,
                                          [Description("Section number from get_view_outline")] int page)
    {
        await _ctx.Authorize(endpointId);
        var view = await _ctx.GetViewOrThrow(endpointId, queryId, viewId);

        var code = view.Code ?? String.Empty;
        var sections = DataLinqMcpViewSections.Parse(code);
        var section = sections.FirstOrDefault(s => s.Index == page)
            ?? throw new McpException($"Section {page} not found. Valid sections: 0..{sections.Count - 1}");

        var lines = DataLinqMcpViewSections.SplitLines(code);
        var numbered = String.Join("\n",
            Enumerable.Range(section.StartLine, section.EndLine - section.StartLine + 1)
                      .Select(n => $"{n}: {lines[n - 1].TrimEnd('\r')}"));

        return new
        {
            route = view.Route(),
            page = section.Index,
            kind = section.Kind,
            startLine = section.StartLine,
            endLine = section.EndLine,
            sectionCount = sections.Count,
            code = numbered
        };
    }

    [McpServerTool(Name = "verify_view", ReadOnly = true)]
    [Description("Compiles Razor code for a view without saving it. Returns success or the compiler errors (line + text). Only compilation, NOT layout or runtime data.")]
    async public Task<object> VerifyView(string endpointId, string queryId, string viewId,
                                         [Description("Complete Razor code of the view")] string code,
                                         [Description("true for PDF report views (@PDF.* helper), false for HTML dashboards/pages")] bool pdfReportMode = false)
    {
        await _ctx.Authorize(endpointId);

        var view = new DataLinqEndPointQueryView()
        {
            EndPointId = endpointId,
            QueryId = queryId,
            ViewId = viewId,
            Code = code,
            PDFReportMode = pdfReportMode
        };

        return await _ctx.Compile(view);
    }

    #endregion

    #region Run queries (approval required)

    [McpServerTool(Name = "sample_query", ReadOnly = true)]
    [Description("Runs a saved query and returns the column names (exact, case-sensitive keys of Model.Records) and the first rows. " +
                 "The user must approve the run in an approval dialog shown by the server.")]
    async public Task<object> SampleQuery(RequestContext<CallToolRequestParams> context,
                                          string endpointId, string queryId,
                                          [Description("Optional query string with parameters, e.g. 'id=4711'")] string parameters = null,
                                          int top = 5,
                                          CancellationToken cancellationToken = default)
    {
        await _ctx.Authorize(endpointId);

        var route = $"{endpointId}@{queryId}";
        await _ctx.Approval.EnsureApprovedAsync(context, "run a query", route,
            $"Parameters: {(String.IsNullOrWhiteSpace(parameters) ? "(none)" : parameters)}\nReturns at most {Math.Clamp(top, 0, 50)} rows to the agent.",
            rememberKey: $"run|{route}",
            cancellationToken: cancellationToken);

        var arguments = System.Web.HttpUtility.ParseQueryString(parameters?.TrimStart('?') ?? String.Empty);
        var (result, _, succeeded) = await _ctx.DataLinq.QueryAsync(_ctx.HttpContextAccessor.HttpContext, route, arguments);

        var records = (result as IEnumerable<object>)?
            .OfType<IDictionary<string, object>>()
            .ToArray() ?? Array.Empty<IDictionary<string, object>>();

        return new
        {
            success = succeeded,
            count = records.Length,
            columns = Columns(records),
            rows = records.Take(Math.Clamp(top, 0, 50)).ToArray(),
            error = succeeded ? null : result?.ToString()
        };
    }

    [McpServerTool(Name = "run_sql_preview", ReadOnly = true)]
    [Description("Executes an UNSAVED read-only SQL statement (single SELECT/WITH) against a database endpoint using the endpoint's own credentials and returns columns (name + type) and max 200 rows. " +
                 "Use it to explore schemas (e.g. INFORMATION_SCHEMA, sys.*, all_tab_columns) and to test a statement before create_query/update_query. Nothing is stored. " +
                 "The user must approve every statement in an approval dialog shown by the server.")]
    async public Task<object> RunSqlPreview(RequestContext<CallToolRequestParams> context,
                                            string endpointId,
                                            [Description("Single SELECT or WITH statement. " + StatementSyntax + " Raw {{param}} placeholders are not allowed here.")] string statement,
                                            [Description("Optional query string with parameters, e.g. 'id=4711'")] string parameters = null,
                                            int top = 50,
                                            CancellationToken cancellationToken = default)
    {
        await _ctx.Authorize(endpointId);
        EnsureReadOnlySql(statement);

        var endPoint = await _ctx.Persistance.GetEndPoint(endpointId)
            ?? throw new McpException($"Endpoint {endpointId} not found");

        if (endPoint.TypeValue != (int)DefaultEndPointTypes.Database)
        {
            throw new McpException("run_sql_preview is only supported for database endpoints");
        }

        var engine = _ctx.SelectEngines.FirstOrDefault(e => e.EndpointType == endPoint.TypeValue)
            ?? throw new McpException("No query engine available for this endpoint");

        // every statement is approved individually (no remember option)
        await _ctx.Approval.EnsureApprovedAsync(context, "run a read-only SQL statement", $"endpoint {endpointId}",
            $"SQL:\n{Shorten(statement)}\n\nParameters: {(String.IsNullOrWhiteSpace(parameters) ? "(none)" : parameters)}\nReturns at most {Math.Clamp(top, 0, 200)} rows to the agent.",
            cancellationToken: cancellationToken);

        var query = new DataLinqEndPointQuery()
        {
            EndPointId = endpointId,
            QueryId = "_mcp_preview",
            Statement = statement
        };
        var arguments = System.Web.HttpUtility.ParseQueryString(parameters?.TrimStart('?') ?? String.Empty);

        try
        {
            var (records, _) = await engine.SelectAsync(endPoint, query, arguments);
            var dicts = (records ?? Array.Empty<object>())
                .OfType<IDictionary<string, object>>()
                .ToArray();

            return new
            {
                success = true,
                count = dicts.Length,
                columns = Columns(dicts),
                rows = dicts.Take(Math.Clamp(top, 0, 200)).ToArray()
            };
        }
        catch (Exception ex)
        {
            // never leak connection details
            return new { success = false, error = ex.Message };
        }
    }

    #endregion

    #region Write views (approval required, no delete)

    [McpServerTool(Name = "create_view", Destructive = false)]
    [Description("Creates a new view. The code is compiled first; then the server asks the user for approval. Nothing is stored on compile errors or if the user declines.")]
    async public Task<object> CreateView(RequestContext<CallToolRequestParams> context,
                                         string endpointId, string queryId, string viewId,
                                         string code,
                                         string name = null,
                                         string description = null,
                                         [Description(FlagPdfReportMode)] bool pdfReportMode = false,
                                         CancellationToken cancellationToken = default)
    {
        await _ctx.Authorize(endpointId, Const.CreateViewRoleParameter);

        viewId = viewId.ToValidDataLinqRouteId();

        if (await _ctx.Persistance.GetEndPointQuery(endpointId, queryId) == null)
        {
            throw new McpException($"Query {endpointId}@{queryId} does not exist");
        }
        if (await _ctx.Persistance.GetEndPointQueryView(endpointId, queryId, viewId) != null)
        {
            throw new McpException($"View {endpointId}@{queryId}@{viewId} already exists. Use save_view/patch_view to update it.");
        }

        var view = new DataLinqEndPointQueryView()
        {
            EndPointId = endpointId,
            QueryId = queryId,
            ViewId = viewId,
            Name = name ?? viewId,
            Description = description,
            Code = code,
            PDFReportMode = pdfReportMode,
            Created = DateTime.UtcNow,
            Changed = DateTime.UtcNow
        };

        var compileResult = await _ctx.Compile(view);
        if (compileResult is CompileFailure)
        {
            return compileResult;
        }

        await _ctx.Approval.EnsureApprovedAsync(context, "create a new view", view.Route(),
            $"Type: {(pdfReportMode ? "PDF report" : "HTML page/dashboard")}\nName: {view.Name}\nCode: {DataLinqMcpViewSections.SplitLines(code).Length} lines, compiles successfully.",
            cancellationToken: cancellationToken);

        var success = await _ctx.Persistance.StoreEndPointQueryView(view);
        if (success)
        {
            _ctx.Notification?.ItemCreated(view.Route());
        }

        return new { success, route = view.Route() };
    }

    [McpServerTool(Name = "save_view", Destructive = true)]
    [Description("Overwrites the Razor code of an existing view (other properties are kept). Compiled first; then the server asks the user for approval. " +
                 "Returns the previous code so it can be restored.")]
    async public Task<object> SaveView(RequestContext<CallToolRequestParams> context,
                                       string endpointId, string queryId, string viewId,
                                       string code,
                                       CancellationToken cancellationToken = default)
    {
        await _ctx.Authorize(endpointId);

        var view = await _ctx.GetViewOrThrow(endpointId, queryId, viewId, ". Use create_view.");
        var oldCode = view.Code ?? String.Empty;

        view.EndPointId = endpointId;
        view.QueryId = queryId;
        view.Code = code;
        view.Changed = DateTime.UtcNow;

        var compileResult = await _ctx.Compile(view);
        if (compileResult is CompileFailure)
        {
            return compileResult;
        }

        await _ctx.Approval.EnsureApprovedAsync(context, "overwrite the code of a view", view.Route(),
            $"Full rewrite: {DataLinqMcpViewSections.SplitLines(oldCode).Length} lines -> {DataLinqMcpViewSections.SplitLines(code).Length} lines. Compiles successfully.",
            rememberKey: $"save|{view.Route()}",
            cancellationToken: cancellationToken);

        var success = await _ctx.Persistance.StoreEndPointQueryView(view);
        if (success)
        {
            _ctx.Notification?.ItemUpdated(view.Route());
        }

        return new { success, route = view.Route(), previousCode = oldCode };
    }

    [McpServerTool(Name = "patch_view", Destructive = true)]
    [Description("Replaces ONE exact text snippet in the Razor code of an existing view and saves it (token-efficient alternative to save_view). " +
                 "oldText must occur exactly once (within 'page' if given, see get_view_outline). The resulting code is compiled first; " +
                 "then the server asks the user for approval. Nothing is saved on errors or if the user declines.")]
    async public Task<object> PatchView(RequestContext<CallToolRequestParams> context,
                                        string endpointId, string queryId, string viewId,
                                        [Description("Exact existing text to replace (without line number prefixes). Must be unique.")] string oldText,
                                        [Description("Replacement text (may be empty to remove oldText)")] string newText,
                                        [Description("Optional section number from get_view_outline to restrict the search")] int? page = null,
                                        CancellationToken cancellationToken = default)
    {
        await _ctx.Authorize(endpointId);

        if (String.IsNullOrEmpty(oldText))
        {
            throw new McpException("oldText is required");
        }

        var view = await _ctx.GetViewOrThrow(endpointId, queryId, viewId, ". Use create_view.");
        var code = view.Code ?? String.Empty;

        int rangeStart = 0, rangeEnd = code.Length;
        if (page.HasValue)
        {
            var sections = DataLinqMcpViewSections.Parse(code);
            var section = sections.FirstOrDefault(s => s.Index == page.Value)
                ?? throw new McpException($"Section {page} not found. Valid sections: 0..{sections.Count - 1}");
            rangeStart = section.Offset;
            rangeEnd = section.Offset + section.Length;
        }

        var originalCode = code;
        var error = ReplaceUnique(ref code, ref oldText, ref newText, rangeStart, rangeEnd,
                                  page.HasValue ? $" in section {page}" : "", out var index);
        if (error != null)
        {
            return new { success = false, error };
        }

        view.EndPointId = endpointId;
        view.QueryId = queryId;
        view.Code = code;
        view.Changed = DateTime.UtcNow;

        var compileResult = await _ctx.Compile(view);
        if (compileResult is CompileFailure)
        {
            return compileResult;
        }

        var line = originalCode.Take(index).Count(c => c == '\n') + 1;
        await _ctx.Approval.EnsureApprovedAsync(context, "change a view", view.Route(),
            $"Line {line}:\n--- old\n{Shorten(oldText)}\n+++ new\n{Shorten(newText)}",
            rememberKey: $"save|{view.Route()}",
            cancellationToken: cancellationToken);

        var success = await _ctx.Persistance.StoreEndPointQueryView(view);
        if (success)
        {
            _ctx.Notification?.ItemUpdated(view.Route());
        }

        return new { success, route = view.Route(), line, sectionCount = DataLinqMcpViewSections.Parse(view.Code).Count };
    }

    [McpServerTool(Name = "update_view", Destructive = true)]
    [Description("Updates properties of an existing view. Only passed (non-null) values are changed. Can also replace the complete view CSS/JS " +
                 "(for small CSS/JS changes prefer patch_view_asset). Code is compiled first; then the server asks the user for approval.")]
    async public Task<object> UpdateView(RequestContext<CallToolRequestParams> context,
                                         string endpointId, string queryId, string viewId,
                                         string code = null,
                                         string name = null,
                                         string description = null,
                                         [Description("URL parameters used for previews in DataLinq Code, e.g. 'id=4711'")] string testParameters = null,
                                         [Description("Comma separated JS libraries the instance provides for this view")] string includedJsLibraries = null,
                                         [Description("View rendered on exceptions (empty = global error page)")] string errorPage = null,
                                         [Description("Allow users to see the view source")] bool? showCode = null,
                                         [Description(FlagPdfReportMode)] bool? pdfReportMode = null,
                                         [Description(FlagPdfCompatibilityNotice)] bool? pdfCompatibilityNotice = null,
                                         [Description(FlagPdfReportEditing)] bool? pdfReportEditing = null,
                                         [Description(FlagCacheToken)] int? cacheTokenTTLDays = null,
                                         [Description(FlagCacheToken)] int? cacheTokenTTLHours = null,
                                         [Description(FlagCacheToken)] int? cacheTokenTTLMinutes = null,
                                         [Description(FlagCacheToken)] int? cacheTokenMaxUsage = null,
                                         [Description("Complete view CSS (replaces existing)")] string css = null,
                                         [Description("Complete view JavaScript (replaces existing)")] string js = null,
                                         CancellationToken cancellationToken = default)
    {
        await _ctx.Authorize(endpointId);

        var view = await _ctx.GetViewOrThrow(endpointId, queryId, viewId, ". Use create_view.");

        var changes = new StringBuilder();
        void Track(string prop, object value) { if (value != null) changes.Append($"- {prop}: {Shorten(value.ToString(), 200)}\n"); }
        Track("code", code == null ? null : $"{DataLinqMcpViewSections.SplitLines(code).Length} lines");
        Track("name", name); Track("description", description); Track("testParameters", testParameters);
        Track("includedJsLibraries", includedJsLibraries); Track("errorPage", errorPage); Track("showCode", showCode);
        Track("pdfReportMode", pdfReportMode); Track("pdfCompatibilityNotice", pdfCompatibilityNotice); Track("pdfReportEditing", pdfReportEditing);
        Track("cacheTokenTTLDays", cacheTokenTTLDays); Track("cacheTokenTTLHours", cacheTokenTTLHours);
        Track("cacheTokenTTLMinutes", cacheTokenTTLMinutes); Track("cacheTokenMaxUsage", cacheTokenMaxUsage);
        Track("css", css == null ? null : $"replaced ({css.Length} chars)");
        Track("js", js == null ? null : $"replaced ({js.Length} chars)");

        view.EndPointId = endpointId;
        view.QueryId = queryId;
        view.Code = code ?? view.Code;
        view.Name = name ?? view.Name;
        view.Description = description ?? view.Description;
        view.TestParameters = testParameters ?? view.TestParameters;
        view.IncludedJsLibraries = includedJsLibraries ?? view.IncludedJsLibraries;
        view.ErrorPage = errorPage ?? view.ErrorPage;
        view.ShowCode = showCode ?? view.ShowCode;
        view.PDFReportMode = pdfReportMode ?? view.PDFReportMode;
        view.PDFCompatibilityNotice = pdfCompatibilityNotice ?? view.PDFCompatibilityNotice;
        view.PDFReportEditing = pdfReportEditing ?? view.PDFReportEditing;
        view.CacheTokenTTL_days = cacheTokenTTLDays ?? view.CacheTokenTTL_days;
        view.CacheTokenTTL_hours = cacheTokenTTLHours ?? view.CacheTokenTTL_hours;
        view.CacheTokenTTL_minutes = cacheTokenTTLMinutes ?? view.CacheTokenTTL_minutes;
        view.CacheTokenMaxUsage = cacheTokenMaxUsage ?? view.CacheTokenMaxUsage;
        view.Changed = DateTime.UtcNow;

        var compileResult = await _ctx.Compile(view);
        if (compileResult is CompileFailure)
        {
            return compileResult;
        }

        await _ctx.Approval.EnsureApprovedAsync(context, "change view properties", view.Route(),
            changes.Length > 0 ? "Changes:\n" + changes : "No property changes (re-save).",
            cancellationToken: cancellationToken);

        var success = await _ctx.Persistance.StoreEndPointQueryView(view);
        if (success && css != null)
        {
            success = await _ctx.Persistance.StoreViewCss(view.Route(), css);
        }
        if (success && js != null)
        {
            success = await _ctx.Persistance.StoreViewJs(view.Route(), js);
        }
        if (success)
        {
            _ctx.Notification?.ItemUpdated(view.Route());
        }

        return new { success, route = view.Route() };
    }

    #endregion

    #region Write queries (approval required, no delete)

    [McpServerTool(Name = "create_query", Destructive = false)]
    [Description("Creates a new query. Test the statement with run_sql_preview first. The server asks the user for approval before storing. " + StatementSyntax)]
    async public Task<object> CreateQuery(RequestContext<CallToolRequestParams> context,
                                          string endpointId, string queryId, string statement,
                                          string name = null,
                                          string description = null,
                                          string testParameters = null,
                                          [Description("Users/roles allowed to run the query. Default: current user")] string[] access = null,
                                          string[] accessTokens = null,
                                          DataLinqEndPointQuery.Domain[] domains = null,
                                          DataLinqEndPointQuery.JsonApiHttpHeader[] jsonApiHttpHeaders = null,
                                          CancellationToken cancellationToken = default)
    {
        await _ctx.Authorize(endpointId, Const.CreateQueryRoleParameter);

        queryId = queryId.ToValidDataLinqRouteId();

        if (await _ctx.Persistance.GetEndPoint(endpointId) == null)
        {
            throw new McpException($"Endpoint {endpointId} does not exist");
        }
        if (await _ctx.Persistance.GetEndPointQuery(endpointId, queryId) != null)
        {
            throw new McpException($"Query {endpointId}@{queryId} already exists. Use update_query.");
        }

        var query = new DataLinqEndPointQuery()
        {
            EndPointId = endpointId,
            QueryId = queryId,
            Name = name ?? queryId,
            Description = description,
            Statement = statement,
            TestParameters = testParameters,
            Access = access ?? new[] { _ctx.Identity().Name },
            AccessTokens = accessTokens,
            Domains = domains,
            JsonApiHttpHeaders = jsonApiHttpHeaders,
            Created = DateTime.UtcNow,
            Changed = DateTime.UtcNow
        };

        await _ctx.Approval.EnsureApprovedAsync(context, "create a new query", query.Route,
            $"Statement:\n{Shorten(statement)}\n\nAccess: {String.Join(", ", query.Access ?? Array.Empty<string>())}",
            cancellationToken: cancellationToken);

        var success = await _ctx.Persistance.StoreEndPointQuery(query);
        if (success)
        {
            _ctx.Notification?.ItemCreated(query.Route);
        }

        return new { success, route = query.Route };
    }

    [McpServerTool(Name = "update_query", Destructive = true)]
    [Description(StatementSyntax + " Updates an existing query. Only passed (non-null) values are changed; arrays replace existing ones. " +
                 "The server asks the user for approval and lists the views that depend on this query. Returns the previous statement.")]
    async public Task<object> UpdateQuery(RequestContext<CallToolRequestParams> context,
                                          string endpointId, string queryId,
                                          string statement = null,
                                          string name = null,
                                          string description = null,
                                          string testParameters = null,
                                          bool? showCode = null,
                                          string[] access = null,
                                          string[] accessTokens = null,
                                          DataLinqEndPointQuery.Domain[] domains = null,
                                          DataLinqEndPointQuery.JsonApiHttpHeader[] jsonApiHttpHeaders = null,
                                          CancellationToken cancellationToken = default)
    {
        await _ctx.Authorize(endpointId);

        var query = await _ctx.Persistance.GetEndPointQuery(endpointId, queryId)
            ?? throw new McpException($"Query {endpointId}@{queryId} not found. Use create_query.");

        if (accessTokens != null && accessTokens.Any(t => t == "***"))
        {
            throw new McpException("accessTokens contains masked values (***). Omit accessTokens to keep the existing tokens.");
        }

        var previousStatement = query.Statement;
        var views = (await _ctx.Persistance.GetViewIds(endpointId, queryId))?.ToArray() ?? Array.Empty<string>();

        var details = new StringBuilder();
        if (statement != null)
        {
            details.Append($"--- old statement\n{Shorten(previousStatement, 1200)}\n+++ new statement\n{Shorten(statement, 1200)}\n");
        }
        if (name != null) details.Append($"- name: {name}\n");
        if (description != null) details.Append($"- description changed\n");
        if (testParameters != null) details.Append($"- testParameters: {testParameters}\n");
        if (showCode != null) details.Append($"- showCode: {showCode}\n");
        if (access != null) details.Append($"- access: {String.Join(", ", access)}\n");
        if (accessTokens != null) details.Append($"- accessTokens replaced ({accessTokens.Length})\n");
        if (domains != null) details.Append($"- domains replaced ({domains.Length})\n");
        if (jsonApiHttpHeaders != null) details.Append($"- JSON API headers replaced ({jsonApiHttpHeaders.Length})\n");
        details.Append($"\nViews using this query ({views.Length}): {(views.Length == 0 ? "none" : String.Join(", ", views))}");

        query.EndPointId = endpointId;
        query.Statement = statement ?? query.Statement;
        query.Name = name ?? query.Name;
        query.Description = description ?? query.Description;
        query.TestParameters = testParameters ?? query.TestParameters;
        query.ShowCode = showCode ?? query.ShowCode;
        query.Access = access ?? query.Access;
        query.AccessTokens = accessTokens ?? query.AccessTokens;
        query.Domains = domains ?? query.Domains;
        query.JsonApiHttpHeaders = jsonApiHttpHeaders ?? query.JsonApiHttpHeaders;
        query.Changed = DateTime.UtcNow;

        await _ctx.Approval.EnsureApprovedAsync(context, "change a query", query.Route, details.ToString(),
            cancellationToken: cancellationToken);

        var success = await _ctx.Persistance.StoreEndPointQuery(query);
        if (success)
        {
            _ctx.Notification?.ItemUpdated(query.Route);
        }

        return new { success, route = query.Route, previousStatement };
    }

    #endregion

    #region Preview (approval required: renders the view and runs its queries)

    [McpServerTool(Name = "get_preview_link", ReadOnly = true)]
    [Description("Returns a short-lived, route-bound URL that renders the SAVED view in a browser without login (this runs the view's queries, so the server asks the user for approval; " +
                 "the user can allow it for the rest of the session). Open it with your browser/Playwright tool, take screenshots and run the render checks BEFORE presenting the result to the user.")]
    async public Task<object> GetPreviewLink(RequestContext<CallToolRequestParams> context,
                                             string endpointId, string queryId, string viewId,
                                             [Description("Optional query string with view parameters, e.g. 'id=4711&year=2024'")] string parameters = null,
                                             CancellationToken cancellationToken = default)
    {
        await _ctx.Authorize(endpointId);
        await _ctx.GetViewOrThrow(endpointId, queryId, viewId);

        var route = $"{endpointId}@{queryId}@{viewId}";
        await _ctx.Approval.EnsureApprovedAsync(context, "render a preview of a view (runs its queries)", route,
            $"Parameters: {(String.IsNullOrWhiteSpace(parameters) ? "(none)" : parameters)}",
            rememberKey: $"preview|{route}",
            cancellationToken: cancellationToken);

        var token = _ctx.Store.IssuePreviewToken(_ctx.Identity(), route, _ctx.McpOptions.PreviewLinkLifetimeSeconds);
        var http = _ctx.HttpContextAccessor.HttpContext;

        var url = $"{http.Request.Scheme}://{http.Request.Host}{http.Request.PathBase}/datalinq/report/{Uri.EscapeDataString(route)}" +
                  $"?{DataLinqMcpPreview.QueryParameter}={Uri.EscapeDataString(token)}";

        if (!String.IsNullOrWhiteSpace(parameters))
        {
            url += "&" + parameters.TrimStart('?', '&');
        }

        return new
        {
            url,
            expiresInSeconds = _ctx.McpOptions.PreviewLinkLifetimeSeconds,
            note = "Preview links are single use and must be opened within expiresInSeconds; request a new link to reload. Only for this route, Test/Development only."
        };
    }

    #endregion
}
