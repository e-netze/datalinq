using E.DataLinq.Web.Razor;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace E.DataLinq.Web.Mcp;

// Tools only needed by the PDF report skills.
[McpServerToolType]
public class DataLinqMcpPdfTools
{
    private readonly DataLinqMcpToolContext _ctx;

    public DataLinqMcpPdfTools(DataLinqMcpToolContext ctx)
    {
        _ctx = ctx;
    }

    [McpServerTool(Name = "get_pdf_reference", ReadOnly = true)]
    [Description("Returns the documented @PDF.* (report layout) and SECURITY.* helper methods from the DataLinq XML documentation. " +
                 "Without 'method'/'search' only names and one-line summaries are returned (cheap). Never use PDF methods that are not listed here.")]
    public object GetPdfReference([Description("'PDF' (default) or 'SECURITY'")] string helper = "PDF",
                                  [Description("Optional exact method name for full signature(s)")] string method = null,
                                  [Description("Optional text to search in method names/descriptions")] string search = null)
    {
        _ctx.Identity();

        var help = helper?.Trim().ToUpperInvariant() == "SECURITY"
            ? DataLinqMcpHelperReference.Get(typeof(DataLinqSecurityHelper), "SECURITY")
            : DataLinqMcpHelperReference.Get(typeof(DataLinqPdfHelper), "PDF");

        return DataLinqMcpHelperReference.ToResult(help, method, search);
    }
}
