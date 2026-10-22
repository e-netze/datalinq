using ModelContextProtocol.Server;
using System;
using System.ComponentModel;

namespace E.DataLinq.Web.Mcp;

// Statement syntax per endpoint type (source: DataLinqExtensions.ParseStatement, StringExtensions.ReplacePlaceholders, engines).
[McpServerToolType]
public class DataLinqMcpQueryTools
{
    private readonly DataLinqMcpToolContext _ctx;

    public DataLinqMcpQueryTools(DataLinqMcpToolContext ctx)
    {
        _ctx = ctx;
    }

    private const string Directives =
        "#if / #endif (all endpoint types using DataLinq statement parsing):\n" +
        "- '#if a,b' must start the line; parameters comma separated without spaces. The lines up to the matching '#endif' are kept only if ALL listed URL parameters exist and are not empty/whitespace.\n" +
        "- Blocks can be nested (inner block is off when the outer is off). There is no #else and no negation. Missing #endif or extra #endif -> 'ParseStatement: Syntax error'.\n" +
        "- Once a statement contains '#if ', lines are trimmed and re-joined with newlines (do not rely on indentation).\n" +
        "Patterns:\n" +
        "  Optional filter:  WHERE 1=1\\n#if x\\nAND col = @x\\n#endif\n" +
        "  Always exactly one row (empty when id is missing):\n" +
        "    SELECT u.* FROM (SELECT 1 AS x) d LEFT JOIN Users u ON 1 = 0\\n#if id\\nOR u.UserId = @id\\n#endif\n";

    private const string Database =
        "Database endpoints (SQL Server, Oracle, PostgreSQL, SQLite ...):\n" +
        "- '@name' or ':name' = bound command parameter, created for every URL parameter whose name appears in the statement (preferred, safe).\n" +
        "- A '@name' in the statement WITHOUT a matching URL parameter is passed to the database unbound -> provider error (e.g. 'Insufficient parameters', 'Must declare the scalar variable'). Guard it with #if or make the parameter required.\n" +
        "- '{{name}}' = raw text replacement of the value (SQL injection risk; only for identifiers/lists; comma separated values are processed). Not allowed in run_sql_preview.\n" +
        "- '_orderby': '@_orderby' (e.g. 'ORDER BY @_orderby') is replaced by validated column names from the _orderby parameter; '-COL' = DESC. DLH.GetRecordsAsync(orderby:) and SortView use it.\n" +
        "- Parameter values come from the URL query string (or DLH.GetRecordsAsync filter 'A=1&B=2').\n";

    private const string Cypher =
        "Cypher (graph) endpoints: same #if handling as SQL; parameters as supported by the Cypher engine.\n";

    private const string TextFile =
        "TextFile endpoints: statement is parsed with #if, then '{{name}}' placeholders are replaced by URL parameter values; missing placeholders stay as-is.\n";

    private const string JsonApi =
        "JsonApi endpoints: the statement is the request URL/path; #if blocks are applied, then '{{name}}' placeholders are replaced (values are not URL-encoded automatically - keep them simple). HTTP headers are configured on the query (jsonApiHttpHeaders).\n";

    private const string PlainText =
        "PlainText endpoints: the statement is returned/processed by the PlainText engine with its own placeholder parsing; check an existing query of the endpoint with get_query.\n";

    [McpServerTool(Name = "get_query_reference", ReadOnly = true)]
    [Description("Returns the DataLinq query statement syntax (#if/#endif, @param, {{param}}, _orderby, typical errors) for an endpoint type. " +
                 "Call before writing or changing a query statement.")]
    public object GetQueryReference([Description("Endpoint type: 'database' (default), 'cypher', 'textfile', 'jsonapi', 'plaintext' or 'all'")] string endpointType = "database")
    {
        _ctx.Identity();

        var type = (endpointType ?? "database").Trim().ToLowerInvariant();
        var text = type switch
        {
            "cypher" => Directives + Cypher,
            "textfile" or "text" => Directives + TextFile,
            "jsonapi" or "api" => Directives + JsonApi,
            "plaintext" => PlainText,
            "all" => Directives + Database + Cypher + TextFile + JsonApi + PlainText,
            _ => Directives + Database
        };

        return new { endpointType = type, syntax = text };
    }
}
