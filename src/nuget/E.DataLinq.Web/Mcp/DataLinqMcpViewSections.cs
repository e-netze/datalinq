using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace E.DataLinq.Web.Mcp;

public class DataLinqMcpViewSection
{
    public int Index { get; set; }
    public string Kind { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public int Offset { get; set; }
    public int Length { get; set; }
    public string Summary { get; set; }
}

// Splits view code into sections for token-efficient reading/patching.
// PDF report views (with @PDF.NewPage) are split into pages, all other views into top-level HTML/Razor blocks.
static public class DataLinqMcpViewSections
{
    public const string ModePdf = "pdf-pages";
    public const string ModeHtml = "html-sections";

    static private readonly Regex HeadingRegex = new(@"<h[1-6][^>]*>(.*?)</h[1-6]>", RegexOptions.IgnoreCase);
    static private readonly Regex CommentRegex = new(@"@\*\s*(.*?)\s*\*@");
    static private readonly Regex IdOrClassRegex = new(@"\b(id|class)\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
    static private readonly Regex DlhCallRegex = new(@"\b(DLH|DataLinqHelper|SECURITY|PDF)\.(\w+)\(");
    static private readonly Regex RouteRegex = new(@"[""']([\w\-]+@[\w\-]+(?:@[\w\-]+)?)[""']");

    static private readonly Regex HtmlBoundaryRegex = new(
        @"^(<(style|script|section|div|header|footer|main|nav|article|aside|table|form|ul|ol|h[1-6]|link)\b|@\{|@\*|@foreach\b|@for\b|@if\b|@switch\b|@functions\b|@section\b|@await\b|@DLH\.|@Html\.)",
        RegexOptions.IgnoreCase);

    static public string[] SplitLines(string code) => (code ?? String.Empty).Split('\n');

    static public string DetectMode(string code)
        => (code ?? String.Empty).Contains("PDF.NewPage(") ? ModePdf : ModeHtml;

    static public List<DataLinqMcpViewSection> Parse(string code, string mode = null)
        => (mode ?? DetectMode(code)) == ModePdf ? ParsePdf(code) : ParseHtml(code);

    #region PDF

    static public List<DataLinqMcpViewSection> ParsePdf(string code)
    {
        var lines = SplitLines(code);
        var starts = new List<int>();
        var ends = new List<int>();
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("PDF.NewPage(")) starts.Add(i);
            if (lines[i].Contains("PDF.EndPage(")) ends.Add(i);
        }

        var ranges = new List<(int from, int to, string kind)>();
        if (starts.Count == 0)
        {
            ranges.Add((0, lines.Length - 1, "code"));
        }
        else
        {
            ranges.Add((0, starts[0] - 1, "before-pages"));
            int from = starts[0];
            for (int p = 0; p < starts.Count; p++)
            {
                int next = p + 1 < starts.Count ? starts[p + 1] : lines.Length;
                int end = ends.Where(e => e >= starts[p] && e < next).DefaultIfEmpty(next - 1).Max();
                if (p + 1 < starts.Count)
                {
                    end = Math.Max(end, from);
                    ranges.Add((from, end, "page"));
                    from = end + 1;
                }
                else
                {
                    ranges.Add((from, end, "page"));
                    ranges.Add((end + 1, lines.Length - 1, "after-pages"));
                }
            }
        }

        return Build(lines, ranges, keepEmpty: kind => kind == "page" || kind == "before-pages");
    }

    #endregion

    #region HTML / Razor

    static public List<DataLinqMcpViewSection> ParseHtml(string code)
    {
        var lines = SplitLines(code);
        var boundaries = new List<int>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.Length == 0 || Char.IsWhiteSpace(line[0]) || !HtmlBoundaryRegex.IsMatch(line))
            {
                continue;
            }

            // keep a leading @* comment *@ together with the block it describes
            if (boundaries.Count > 0 && boundaries[^1] == i - 1 && lines[i - 1].TrimStart().StartsWith("@*"))
            {
                continue;
            }

            boundaries.Add(i);
        }

        var ranges = new List<(int from, int to, string kind)>();
        if (boundaries.Count == 0)
        {
            ranges.Add((0, lines.Length - 1, "code"));
        }
        else
        {
            if (boundaries[0] > 0)
            {
                ranges.Add((0, boundaries[0] - 1, "header"));
            }
            for (int b = 0; b < boundaries.Count; b++)
            {
                int from = boundaries[b];
                int to = b + 1 < boundaries.Count ? boundaries[b + 1] - 1 : lines.Length - 1;
                ranges.Add((from, to, HtmlKind(lines, from)));
            }
        }

        return Build(lines, ranges, keepEmpty: _ => false);
    }

    static private string HtmlKind(string[] lines, int from)
    {
        var line = lines[from].Trim();
        if (line.StartsWith("@*") && from + 1 < lines.Length)
        {
            line = lines[from + 1].Trim();
        }

        var lower = line.ToLowerInvariant();
        if (lower.StartsWith("<style")) return "style";
        if (lower.StartsWith("<script")) return "script";
        if (lower.StartsWith("<link")) return "link";
        if (lower.StartsWith("@{") || lower.StartsWith("@functions")) return "razor-code";
        if (lower.StartsWith("@foreach") || lower.StartsWith("@for")) return "loop";
        if (lower.StartsWith("@if") || lower.StartsWith("@switch")) return "condition";
        if (lower.StartsWith("@*")) return "comment";
        if (lower.StartsWith("@await") || lower.StartsWith("@dlh.") || lower.StartsWith("@html.")) return "helper";

        var tag = Regex.Match(lower, @"^<(\w+)");
        return tag.Success ? "html:" + tag.Groups[1].Value : "code";
    }

    #endregion

    static private List<DataLinqMcpViewSection> Build(string[] lines,
                                                       List<(int from, int to, string kind)> ranges,
                                                       Func<string, bool> keepEmpty)
    {
        var offsets = new int[lines.Length + 1];
        for (int i = 0; i < lines.Length; i++)
        {
            offsets[i + 1] = offsets[i] + lines[i].Length + 1;
        }

        var result = new List<DataLinqMcpViewSection>();
        foreach (var r in ranges)
        {
            if (r.to < r.from && !keepEmpty(r.kind))
            {
                continue;
            }

            int to = Math.Max(r.to, r.from - 1);
            var sectionLines = lines.Skip(r.from).Take(to - r.from + 1).ToArray();
            result.Add(new DataLinqMcpViewSection()
            {
                Index = result.Count,
                Kind = r.kind,
                StartLine = r.from + 1,
                EndLine = to + 1,
                Offset = offsets[Math.Min(r.from, lines.Length)],
                Length = Math.Max(0, offsets[to + 1] - offsets[r.from] - (to + 1 == lines.Length ? 1 : 0)),
                Summary = Summarize(sectionLines)
            });
        }

        return result;
    }

    static private string Summarize(string[] lines)
    {
        var parts = new List<string>();
        var text = String.Join("\n", lines);

        var comment = CommentRegex.Match(text);
        if (comment.Success) parts.Add(comment.Groups[1].Value);

        var newPage = lines.FirstOrDefault(l => l.Contains("PDF.NewPage("))?.Trim();
        if (newPage != null) parts.Add(newPage);

        var heading = HeadingRegex.Match(text);
        if (heading.Success) parts.Add("heading: " + Regex.Replace(heading.Groups[1].Value, "<[^>]+>", "").Trim());

        var idOrClass = IdOrClassRegex.Match(text);
        if (idOrClass.Success) parts.Add($"{idOrClass.Groups[1].Value.ToLowerInvariant()}={idOrClass.Groups[2].Value}");

        var helpers = DlhCallRegex.Matches(text).Select(m => $"{m.Groups[1].Value}.{m.Groups[2].Value}").Distinct().Take(5).ToArray();
        if (helpers.Length > 0) parts.Add("calls: " + String.Join(", ", helpers));

        var routes = RouteRegex.Matches(text).Select(m => m.Groups[1].Value).Distinct().Take(3).ToArray();
        if (routes.Length > 0) parts.Add("routes: " + String.Join(", ", routes));

        if (text.Contains("<style", StringComparison.OrdinalIgnoreCase)) parts.Add("contains <style>");
        if (text.Contains("<script", StringComparison.OrdinalIgnoreCase)) parts.Add("contains <script>");
        if (text.Contains("Model.Records")) parts.Add("uses Model.Records");
        if (text.Contains("PDF.BeginReport(")) parts.Add("BeginReport");
        if (text.Contains("PDF.EndReport(")) parts.Add("EndReport");
        if (text.Contains("foreach")) parts.Add("loop");

        var summary = String.Join(" | ", parts);
        return summary.Length > 200 ? summary.Substring(0, 200) + "..." : summary;
    }
}
