using ModelContextProtocol;
using E.DataLinq.Core;
using E.DataLinq.Core.Engines.Abstraction;
using E.DataLinq.Core.Exceptions;
using E.DataLinq.Core.Extensions;
using E.DataLinq.Core.Models;
using E.DataLinq.Core.Models.Authentication;
using E.DataLinq.Core.Services.Abstraction;
using E.DataLinq.Core.Services.Persistance.Abstraction;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Mcp;

// Shared dependencies and helpers for all DataLinq MCP tool classes (shared, PDF, dashboard).
public class DataLinqMcpToolContext
{
    public DataLinqMcpToolContext(IHttpContextAccessor httpContextAccessor,
                                  IPersistanceProviderService persistance,
                                  DataLinqCompilerService compiler,
                                  DataLinqMcpTokenStore store,
                                  DataLinqMcpApproval approval,
                                  IOptions<DataLinqMcpOptions> mcpOptions,
                                  IOptions<DataLinqOptions> options,
                                  DataLinqService datalinq,
                                  IEnumerable<IDataLinqSelectEngine> selectEngines,
                                  IDataLinqApiNotificationService notification = null)
    {
        HttpContextAccessor = httpContextAccessor;
        Persistance = persistance;
        Compiler = compiler;
        Store = store;
        Approval = approval;
        McpOptions = mcpOptions.Value;
        Options = options.Value;
        DataLinq = datalinq;
        SelectEngines = selectEngines;
        Notification = notification;
    }

    public IHttpContextAccessor HttpContextAccessor { get; }
    public IPersistanceProviderService Persistance { get; }
    public DataLinqCompilerService Compiler { get; }
    public DataLinqMcpTokenStore Store { get; }
    public DataLinqMcpApproval Approval { get; }
    public DataLinqMcpOptions McpOptions { get; }
    public DataLinqOptions Options { get; }
    public DataLinqService DataLinq { get; }
    public IEnumerable<IDataLinqSelectEngine> SelectEngines { get; }
    public IDataLinqApiNotificationService Notification { get; }

    #region Identity / Authorization

    public DataLinqCodeIdentity Identity()
    {
        DataLinqMcpEnvironment.EnsureMcpAllowed(Options.EnvironmentType);

        var user = HttpContextAccessor.HttpContext?.User;
        var identity = new DataLinqCodeIdentity()
        {
            Id = user?.GetUserId(),
            Name = user?.TryGetUsername(),
            Roles = user?.GetRoles() ?? Array.Empty<string>()
        };

        if (String.IsNullOrEmpty(identity.Name) || !identity.HasDataLinqCodeRole())
        {
            throw new McpException("Not authorized");
        }

        return identity;
    }

    // same rules as DataLinqCodeApiController.SecureMethodHandler
    async public Task Authorize(string endpointId, params string[] additionalRoleParameters)
    {
        var identity = Identity();
        var required = new[] { endpointId }.Concat(additionalRoleParameters ?? Array.Empty<string>()).ToArray();

        if (identity.HasRoleParameters(required))
        {
            return;
        }

        if (identity.Name.Equals(await Persistance.EndPointCreator(endpointId), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new McpException("Not authorized");
    }

    async public Task<DataLinqEndPointQueryView> GetViewOrThrow(string endpointId, string queryId, string viewId, string hint = null)
        => await Persistance.GetEndPointQueryView(endpointId, queryId, viewId)
           ?? throw new McpException($"View {endpointId}@{queryId}@{viewId} not found{hint}");

    #endregion

    #region Compile

    public class CompileFailure
    {
        public bool Success => false;
        public string ErrorMessage { get; set; }
        public IEnumerable<object> CompilerErrors { get; set; }
    }

    static private readonly Regex EmbeddedImageRegex =
        new(@"data:image/|base64,|FromBase64String|<svg[\s>]", RegexOptions.IgnoreCase);

    // Agents must not embed images (token cost, fake logos). Images come from DataLinq helpers or placeholders.
    static public string CheckImagePolicy(string code)
    {
        if (String.IsNullOrEmpty(code) || !EmbeddedImageRegex.IsMatch(code))
        {
            return null;
        }

        return "Rejected: embedded images (data:image/base64/inline SVG) are not allowed in agent-written views. " +
               "Do NOT embed, extract or redraw logos/images (also not with CSS shapes). " +
               "Use @await DLH.GetImage(baseUrl, path, ...) for public images, @await DLH.GetAgsImage(...) or @await SECURITY.GetAgsImage(...) for ArcGIS/gView maps, " +
               "or a same-sized placeholder box (dashed border, text 'Logo'). Ask the user which option and for the image URL.";
    }

    async public Task<object> Compile(DataLinqEndPointQueryView view)
    {
        var imageError = CheckImagePolicy(view.Code);
        if (imageError != null)
        {
            return new CompileFailure() { ErrorMessage = imageError };
        }

        try
        {
            await Compiler.ValidateRazorCode(view);
            return new { success = true };
        }
        catch (RazorCompileException ex)
        {
            return new CompileFailure()
            {
                ErrorMessage = "Razor compiler errors",
                CompilerErrors = ex.CompilerErrors?.Select(e => (object)new { line = e.Line, error = e.ErrorText })
            };
        }
        catch (Exception ex)
        {
            return new CompileFailure() { ErrorMessage = ex.Message };
        }
    }

    #endregion

    #region Results

    static public object ViewResult(DataLinqEndPointQueryView view)
        => new
        {
            view.EndPointId,
            view.QueryId,
            view.ViewId,
            view.Name,
            view.Description,
            view.PDFReportMode,
            view.PDFCompatibilityNotice,
            view.PDFReportEditing,
            view.ShowCode,
            view.IncludedJsLibraries,
            view.ErrorPage,
            view.CacheTokenTTL_days,
            view.CacheTokenTTL_hours,
            view.CacheTokenTTL_minutes,
            view.CacheTokenMaxUsage,
            view.TestParameters,
            view.Created,
            view.Changed,
            view.Code
        };

    static public object QueryResult(DataLinqEndPointQuery query)
        => new
        {
            query.EndPointId,
            query.QueryId,
            query.Name,
            query.Description,
            query.Statement,
            query.TestParameters,
            query.ShowCode,
            query.Access,
            // secrets are never sent to the agent; update_query keeps existing tokens when accessTokens is omitted
            AccessTokens = query.AccessTokens?.Select(t => "***").ToArray(),
            query.Domains,
            query.JsonApiHttpHeaders,
            query.Created,
            query.Changed
        };

    static public object[] Columns(IEnumerable<IDictionary<string, object>> records)
        => records.SelectMany(r => r)
                  .GroupBy(kv => kv.Key)
                  .Select(g => (object)new
                  {
                      name = g.Key,
                      type = g.Select(kv => kv.Value).FirstOrDefault(v => v != null && v != DBNull.Value)?.GetType().Name ?? "null"
                  })
                  .ToArray();

    #endregion

    #region SQL

    static private readonly Regex ForbiddenSql = new(
        @"\b(insert|update|delete|merge|drop|alter|create|truncate|exec|execute|grant|revoke|deny|into|call|begin|commit|rollback|lock|replace|upsert|copy|vacuum|attach|detach|pragma|dbms_\w*|xp_\w*|sp_\w*)\b",
        RegexOptions.IgnoreCase);

    static public void EnsureReadOnlySql(string statement)
    {
        if (String.IsNullOrWhiteSpace(statement))
        {
            throw new McpException("Statement is empty");
        }

        // {{param}} placeholders are replaced as raw text after validation (SQL injection) => not allowed in previews
        if (statement.Contains("{{"))
        {
            throw new McpException("{{param}} placeholders are not allowed in run_sql_preview. Use @param (bound parameter) instead.");
        }

        var sql = Regex.Replace(statement, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        sql = Regex.Replace(sql, @"--[^\n]*", " ");
        sql = Regex.Replace(sql, @"'([^']|'')*'", "''").Trim().TrimEnd(';').Trim();

        if (sql.Contains(';'))
        {
            throw new McpException("Only a single statement is allowed");
        }
        if (!Regex.IsMatch(sql, @"^(select|with)\b", RegexOptions.IgnoreCase))
        {
            throw new McpException("Only SELECT/WITH statements are allowed");
        }
        var forbidden = ForbiddenSql.Match(sql);
        if (forbidden.Success)
        {
            throw new McpException($"Keyword '{forbidden.Value}' is not allowed in run_sql_preview");
        }
    }

    #endregion

    #region Text helpers

    static public string Shorten(string text, int max = 1500)
        => String.IsNullOrEmpty(text) || text.Length <= max ? text ?? String.Empty : text.Substring(0, max) + "\n...";

    // Replaces exactly one occurrence of oldText inside [rangeStart, rangeEnd). Returns error text or null.
    static public string ReplaceUnique(ref string code, ref string oldText, ref string newText,
                                       int rangeStart, int rangeEnd, string scope, out int index)
    {
        index = -1;
        code ??= String.Empty;
        newText ??= String.Empty;

        if (code.Contains("\r\n"))
        {
            oldText = oldText.Replace("\r\n", "\n").Replace("\n", "\r\n");
            newText = newText.Replace("\r\n", "\n").Replace("\n", "\r\n");
        }

        var matches = new List<int>();
        for (int pos = code.IndexOf(oldText, rangeStart, StringComparison.Ordinal);
             pos >= 0 && pos + oldText.Length <= rangeEnd;
             pos = code.IndexOf(oldText, pos + 1, StringComparison.Ordinal))
        {
            matches.Add(pos);
        }

        if (matches.Count == 0)
        {
            return $"oldText not found{scope}. Re-read the code and copy the text exactly.";
        }
        if (matches.Count > 1)
        {
            return $"oldText matches {matches.Count} times. Add more surrounding lines to make it unique.";
        }

        index = matches[0];
        code = code.Substring(0, index) + newText + code.Substring(index + oldText.Length);
        return null;
    }

    #endregion
}

static internal class DataLinqMcpViewExtensions
{
    static public string Route(this DataLinqEndPointQueryView view)
        => $"{view.EndPointId}@{view.QueryId}@{view.ViewId}";
}
