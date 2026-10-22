using ModelContextProtocol;
using E.DataLinq.Web.Razor;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static E.DataLinq.Web.Mcp.DataLinqMcpToolContext;

namespace E.DataLinq.Web.Mcp;

// Tools only needed by the dashboard/site skills (HTML/CSS/JS + Razor + @DLH helpers).
[McpServerToolType]
public class DataLinqMcpDashboardTools
{
    private readonly DataLinqMcpToolContext _ctx;

    static private readonly Regex RouteLiteralRegex = new(@"[""']([A-Za-z0-9_\-]+@[A-Za-z0-9_\-]+(?:@[A-Za-z0-9_\-]+)?)(?:\?[^""']*)?[""']");
    static private readonly Regex DlhCallRegex = new(@"\b(?:DLH|DataLinqHelper)\.(\w+)\(");

    public DataLinqMcpDashboardTools(DataLinqMcpToolContext ctx)
    {
        _ctx = ctx;
    }

    [McpServerTool(Name = "get_dlh_reference", ReadOnly = true)]
    [Description("Returns the documented DataLinq helper methods from the DataLinq XML documentation: 'DLH' (@DLH.* / DataLinqHelper: tables, filters, sort, " +
                 "include/refresh views, forms, combos, charts, statistics, GetRecordsAsync, images ...) or 'RECORDS' (extension methods on Model.Records / a record, " +
                 "e.g. JsonValue). Without 'method'/'search' only names and one-line summaries are returned (cheap). Never use helper methods that are not listed here.")]
    public object GetDlhReference([Description("'DLH' (default) or 'RECORDS'")] string helper = "DLH",
                                  [Description("Optional exact method name for full signature(s) and parameter descriptions")] string method = null,
                                  [Description("Optional text to search in method names/descriptions, e.g. 'filter', 'chart', 'combo'")] string search = null)
    {
        _ctx.Identity();

        var help = helper?.Trim().ToUpperInvariant() == "RECORDS"
            ? DataLinqMcpHelperReference.Get(typeof(JsonApiRecordExtensions), "Model.Records", recordsExtensionMethods: true)
            : DataLinqMcpHelperReference.Get(typeof(DataLinqHelper), "DLH", includeExtensionMethods: true);

        return DataLinqMcpHelperReference.ToResult(help, method, search);
    }

    [McpServerTool(Name = "list_view_dependencies", ReadOnly = true)]
    [Description("Shows how a view is wired: its own query (Model.Records), other queries/views it references via string routes " +
                 "(e.g. DLH.GetRecordsAsync(\"ep@query\"), DLH.IncludeView(\"ep@query@view\"), FilterView, JsFetchData ...), the DLH methods it calls, " +
                 "and which other views of the same query exist. Read-only, does not run queries.")]
    async public Task<object> ListViewDependencies(string endpointId, string queryId, string viewId)
    {
        await _ctx.Authorize(endpointId);
        var view = await _ctx.GetViewOrThrow(endpointId, queryId, viewId);
        var route = view.Route();

        var css = await _ctx.Persistance.GetViewCss(route) ?? String.Empty;
        var js = await _ctx.Persistance.GetViewJs(route) ?? String.Empty;
        var all = String.Join("\n", view.Code ?? String.Empty, js);

        var references = RouteLiteralRegex.Matches(all)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(r => new { route = r, kind = r.Count(c => c == '@') == 2 ? "view" : "query" })
            .ToArray();

        var dlhCalls = DlhCallRegex.Matches(view.Code ?? String.Empty)
            .Select(m => m.Groups[1].Value)
            .GroupBy(n => n)
            .Select(g => new { method = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToArray();

        var siblings = (await _ctx.Persistance.GetViewIds(endpointId, queryId))?
            .Where(v => !v.Equals(viewId, StringComparison.OrdinalIgnoreCase))
            .ToArray() ?? Array.Empty<string>();

        return new
        {
            route,
            modelRecordsQuery = $"{endpointId}@{queryId}",
            usesModelRecords = (view.Code ?? String.Empty).Contains("Model.Records"),
            pdfReportMode = view.PDFReportMode,
            references,
            dlhCalls,
            includedJsLibraries = view.IncludedJsLibraries,
            cssChars = css.Length,
            jsChars = js.Length,
            otherViewsOfSameQuery = siblings
        };
    }

    [McpServerTool(Name = "get_view_assets", ReadOnly = true)]
    [Description("Returns only the separate view CSS and/or JavaScript (stored next to the Razor code), with line numbers. Cheaper than get_view.")]
    async public Task<object> GetViewAssets(string endpointId, string queryId, string viewId,
                                            [Description("'css', 'js' or 'both' (default)")] string asset = "both")
    {
        await _ctx.Authorize(endpointId);
        var view = await _ctx.GetViewOrThrow(endpointId, queryId, viewId);
        var route = view.Route();
        asset = (asset ?? "both").Trim().ToLowerInvariant();

        return new
        {
            route,
            css = asset is "css" or "both" ? Numbered(await _ctx.Persistance.GetViewCss(route)) : null,
            js = asset is "js" or "both" ? Numbered(await _ctx.Persistance.GetViewJs(route)) : null,
            includedJsLibraries = view.IncludedJsLibraries
        };
    }

    [McpServerTool(Name = "patch_view_asset", Destructive = true)]
    [Description("Replaces ONE exact, unique text snippet in the separate view CSS or JavaScript and saves it. " +
                 "Use oldText='' only to append to an EMPTY asset. The server asks the user for approval before saving.")]
    async public Task<object> PatchViewAsset(RequestContext<CallToolRequestParams> context,
                                             string endpointId, string queryId, string viewId,
                                             [Description("'css' or 'js'")] string asset,
                                             [Description("Exact existing text (without line number prefixes). Must be unique. Empty only if the asset is empty.")] string oldText,
                                             [Description("Replacement text")] string newText,
                                             CancellationToken cancellationToken = default)
    {
        await _ctx.Authorize(endpointId);
        var view = await _ctx.GetViewOrThrow(endpointId, queryId, viewId);
        var route = view.Route();

        asset = (asset ?? String.Empty).Trim().ToLowerInvariant();
        if (asset is not ("css" or "js"))
        {
            throw new McpException("asset must be 'css' or 'js'");
        }

        var code = (asset == "css" ? await _ctx.Persistance.GetViewCss(route) : await _ctx.Persistance.GetViewJs(route)) ?? String.Empty;
        newText ??= String.Empty;

        if (String.IsNullOrEmpty(oldText))
        {
            if (code.Trim().Length > 0)
            {
                return new { success = false, error = $"The {asset} is not empty. Pass a unique oldText (see get_view_assets)." };
            }
            code = newText;
        }
        else
        {
            var error = ReplaceUnique(ref code, ref oldText, ref newText, 0, code.Length, $" in view {asset}", out _);
            if (error != null)
            {
                return new { success = false, error };
            }
        }

        if (CheckImagePolicy(code) is string imageError)
        {
            return new CompileFailure() { ErrorMessage = imageError };
        }

        await _ctx.Approval.EnsureApprovedAsync(context, $"change the view {asset.ToUpperInvariant()}", route,
            $"--- old\n{Shorten(oldText)}\n+++ new\n{Shorten(newText)}",
            rememberKey: $"save|{route}",
            cancellationToken: cancellationToken);

        var success = asset == "css"
            ? await _ctx.Persistance.StoreViewCss(route, code)
            : await _ctx.Persistance.StoreViewJs(route, code);
        if (success)
        {
            _ctx.Notification?.ItemUpdated(route);
        }

        return new { success, route, asset, chars = code.Length };
    }

    static private string Numbered(string text)
    {
        if (String.IsNullOrEmpty(text)) return String.Empty;
        var lines = text.Split('\n');
        return String.Join("\n", lines.Select((l, i) => $"{i + 1}: {l.TrimEnd('\r')}"));
    }
}
