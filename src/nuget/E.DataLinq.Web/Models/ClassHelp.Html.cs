using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace E.DataLinq.Web.Models;

// Html rendering of the DataLinq help (Views/Datalinq/Help.cshtml)
public partial class ClassHelp
{
    #region Public

    /// <summary>
    /// Renders all overloads of a method as cards (signature, examples, description, parameters).
    /// </summary>
    public string ToHtmlString(string methodName, string language = "en")
    {
        var text = HelpTexts.For(language);
        var sb = new StringBuilder();

        var methods = this.Methods.Where(m => m.Name == methodName)
            .Concat(this.ExtensionMethods.Where(m => m.Name == methodName))
            .OrderBy(m => m.Target is null ? 0 : 1)
            .ToList();

        if (methods.Count == 0)
        {
            sb.Append($"<p class='help-empty'>{Encode(text.NotFound)}</p>");
            return sb.ToString();
        }

        var firstShort = ShortText(methods[0]);

        for (int i = 0; i < methods.Count; i++)
        {
            var method = methods[i];

            sb.Append("<section class='help-card'>");

            if (methods.Count > 1)
            {
                sb.Append($"<div class='help-card-label'>{Encode(text.Overload)} {i + 1} / {methods.Count}</div>");

                var shortText = ShortText(method);
                if (i > 0 && !String.IsNullOrWhiteSpace(shortText) && shortText != firstShort)
                {
                    sb.Append($"<p class='help-lead'>{InlineHtml(shortText)}</p>");
                }
            }

            sb.Append($"<h4 class='help-section-title'>{Encode(text.Signature)}</h4>");
            RenderSignature(method, sb);

            sb.Append($"<h4 class='help-section-title'>{Encode(text.Example)}</h4>");
            RenderExample(method, sb, false);
            if (method.Parameters.Any(p => p.HasDefault))
            {
                RenderExample(method, sb, true);
            }

            if (!String.IsNullOrWhiteSpace(method.Description))
            {
                sb.Append($"<h4 class='help-section-title'>{Encode(text.Description)}</h4>");
                sb.Append("<div class='help-text'>");
                sb.Append(DescriptionToHtml(method.Description, text));
                sb.Append("</div>");
            }

            if (method.Parameters.Count > 0)
            {
                sb.Append($"<h4 class='help-section-title'>{Encode(text.Parameters)}</h4>");
                sb.Append("<div class='help-params'>");

                foreach (var parameter in method.Parameters)
                {
                    sb.Append("<div class='help-param'>");
                    sb.Append("<div class='help-param-head'>");
                    sb.Append($"<code class='help-param-name'>{Encode(parameter.Name)}</code>");
                    sb.Append($"<span class='help-chip help-chip-type'>{Encode(TypeName(parameter.ParameterType))}</span>");
                    if (parameter.HasDefault)
                    {
                        sb.Append($"<span class='help-chip help-chip-optional'>{Encode(text.Optional)} = {Encode(DefaultValueText(parameter))}</span>");
                    }
                    sb.Append("</div>");

                    if (!String.IsNullOrWhiteSpace(parameter.Description))
                    {
                        sb.Append("<div class='help-param-text help-text'>");
                        sb.Append(DescriptionToHtml(parameter.Description, text));
                        sb.Append("</div>");
                    }

                    sb.Append("</div>");
                }

                sb.Append("</div>");
            }

            sb.Append("</section>");
        }

        return sb.ToString();
    }

    /// <summary>
    /// The short description of a method: the &lt;short&gt; doc tag or the first sentence of the description.
    /// </summary>
    static public string ShortText(MethodHelp method)
        => !String.IsNullOrWhiteSpace(method?.ShortDescription)
            ? method.ShortDescription.Trim()
            : FirstSentence(method?.Description);

    /// <summary>
    /// The first sentence of a (multi line) description, without examples, notes and code blocks.
    /// </summary>
    static public string FirstSentence(string description, int maxLength = 160)
    {
        if (String.IsNullOrWhiteSpace(description))
        {
            return String.Empty;
        }

        var sb = new StringBuilder();

        foreach (var rawLine in description.Replace("\r", "").Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0)
            {
                if (sb.Length > 0) break;
                continue;
            }

            if (line.StartsWith("((") || KeywordRegex.IsMatch(line))
            {
                if (sb.Length > 0) break;
                continue;
            }

            int codeStart = line.IndexOf("((", StringComparison.Ordinal);
            if (codeStart >= 0)
            {
                line = line.Substring(0, codeStart).Trim();
            }

            if (sb.Length > 0) sb.Append(' ');
            sb.Append(line);

            var match = SentenceEndRegex.Match(sb.ToString());
            if (match.Success)
            {
                return Truncate(sb.ToString().Substring(0, match.Index + 1).Trim(), maxLength);
            }

            if (codeStart >= 0 || EndsSentence(line))
            {
                break;
            }
        }

        return Truncate(sb.ToString().Trim(), maxLength);
    }

    /// <summary>
    /// Renders a doc comment text (one language) as readable html:
    /// paragraphs (lines are joined until a sentence ends), Example/Beispiel lines and ((...)) blocks as copyable code,
    /// Hint/Tipp/Note/Achtung lines as notes, "- " lines as lists and `code` as inline code. All text is html encoded.
    /// </summary>
    static public string DescriptionToHtml(string description, string language = "en")
        => DescriptionToHtml(description, HelpTexts.For(language));

    #endregion

    #region Description Renderer

    static private readonly Regex KeywordRegex = new Regex(
        @"^(?<keyword>Examples?|Beispiele?|Hints?|Tipps?|Tip|Notes?|Hinweise?|Achtung|Warning|Warnung|Important|Wichtig)\s*:\s*(?<rest>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    static private readonly Regex SentenceEndRegex = new Regex(@"[.!?](\s|$)", RegexOptions.Compiled);

    static private bool IsExampleKeyword(string keyword)
        => keyword.StartsWith("Example", StringComparison.OrdinalIgnoreCase)
        || keyword.StartsWith("Beispiel", StringComparison.OrdinalIgnoreCase);

    static private bool IsWarningKeyword(string keyword)
        => keyword.Equals("Achtung", StringComparison.OrdinalIgnoreCase)
        || keyword.StartsWith("Warn", StringComparison.OrdinalIgnoreCase)
        || keyword.Equals("Important", StringComparison.OrdinalIgnoreCase)
        || keyword.Equals("Wichtig", StringComparison.OrdinalIgnoreCase);

    static private bool EndsSentence(string line)
        => line.Length > 0 && ".!?:".IndexOf(line[line.Length - 1]) >= 0;

    static private bool IsLikelyProse(string line)
        => line.Length > 0
        && Char.IsUpper(line[0])
        && EndsSentence(line)
        && line.Contains(' ')
        && line.IndexOfAny(new[] { '@', '(', '<', '{', '=' }) < 0;

    private enum BlockType { Paragraph, Note, Warning, ListItem, Code }

    private sealed class Block
    {
        public BlockType Type;
        public string Label;
        public readonly List<string> Lines = new List<string>();
        public bool Closed;
    }

    static private string DescriptionToHtml(string description, HelpTexts text)
    {
        if (String.IsNullOrWhiteSpace(description))
        {
            return String.Empty;
        }

        var blocks = new List<Block>();
        Block current = null;
        Block code = null;            // open ((...)) block
        bool exampleCode = false;     // open "Example:" block without (( ))
        string pendingLabel = null;   // label for the next code block, e.g. "Example"

        void Close()
        {
            if (current != null)
            {
                current.Closed = true;
                current = null;
            }
            exampleCode = false;
        }

        Block Start(BlockType type, string label = null)
        {
            Close();
            current = new Block { Type = type, Label = label };
            blocks.Add(current);
            return current;
        }

        void AddText(string line)
        {
            if (line.Length == 0)
            {
                return;
            }

            if (current == null || current.Closed || current.Type == BlockType.Code)
            {
                Start(BlockType.Paragraph);
            }

            current.Lines.Add(line);

            if (EndsSentence(line) && current.Type == BlockType.Paragraph)
            {
                Close();
            }
        }

        foreach (var rawLine in description.Replace("\r", "").Split('\n'))
        {
            var line = rawLine.TrimEnd();
            var trimmed = line.Trim();

            if (code != null)
            {
                int end = line.IndexOf("))", StringComparison.Ordinal);
                if (end >= 0)
                {
                    if (line.Substring(0, end).Trim().Length > 0)
                    {
                        code.Lines.Add(line.Substring(0, end));
                    }
                    code = null;
                    current = null;

                    var after = line.Substring(end + 2).Trim();
                    if (after.Trim('.', ',', ';', ' ').Length > 0)
                    {
                        AddText(after);
                    }
                }
                else
                {
                    code.Lines.Add(line);
                }
                continue;
            }

            if (trimmed.Length == 0)
            {
                Close();
                continue;
            }

            // inline or starting ((...)) code block
            int start = trimmed.IndexOf("((", StringComparison.Ordinal);
            if (start >= 0)
            {
                var before = trimmed.Substring(0, start).Trim();
                var keywordMatch = KeywordRegex.Match(before);

                if (keywordMatch.Success && IsExampleKeyword(keywordMatch.Groups["keyword"].Value))
                {
                    pendingLabel = text.Example;
                    var restBefore = keywordMatch.Groups["rest"].Value.Trim();
                    if (restBefore.Length > 0) AddText(restBefore);
                }
                else if (before.Length > 0)
                {
                    // short single line snippet inside a sentence => inline code, e.g. "e.g. ((new string[]{ "a" }))."
                    var innerText = trimmed.Substring(start + 2);
                    int innerEnd = innerText.IndexOf("))", StringComparison.Ordinal);
                    if (innerEnd >= 0 && innerEnd <= 100 && innerText.Substring(0, innerEnd).IndexOf('`') < 0)
                    {
                        AddText($"{before} `{innerText.Substring(0, innerEnd).Trim()}`{innerText.Substring(innerEnd + 2).TrimEnd()}");
                        continue;
                    }

                    AddText(before);
                }

                Close();
                code = Start(BlockType.Code, pendingLabel);
                pendingLabel = null;
                current = null;

                var inner = trimmed.Substring(start + 2);
                int end = inner.IndexOf("))", StringComparison.Ordinal);
                if (end >= 0)
                {
                    if (inner.Substring(0, end).Trim().Length > 0)
                    {
                        code.Lines.Add(inner.Substring(0, end));
                    }
                    code = null;

                    var after = inner.Substring(end + 2).Trim();
                    if (after.Trim('.', ',', ';', ' ').Length > 0) AddText(after);
                }
                else if (inner.Trim().Length > 0)
                {
                    code.Lines.Add(inner);
                }
                continue;
            }

            var match = KeywordRegex.Match(trimmed);
            if (match.Success)
            {
                var keyword = match.Groups["keyword"].Value;
                var rest = match.Groups["rest"].Value.Trim();

                if (IsExampleKeyword(keyword))
                {
                    if (rest.Length > 0)
                    {
                        var block = Start(BlockType.Code, text.Example);
                        block.Lines.Add(rest);
                        Close();
                    }
                    else
                    {
                        Close();
                        pendingLabel = text.Example;
                        exampleCode = true;
                        current = null;
                    }
                }
                else
                {
                    var block = Start(IsWarningKeyword(keyword) ? BlockType.Warning : BlockType.Note, keyword);
                    if (rest.Length > 0)
                    {
                        block.Lines.Add(rest);
                        if (EndsSentence(rest))
                        {
                            Close();
                        }
                    }
                }
                continue;
            }

            // lines after an "Example:" line without (( )) are code until a blank line, the next keyword or prose
            if (exampleCode && !IsLikelyProse(trimmed))
            {
                if (current == null || current.Type != BlockType.Code)
                {
                    var block = new Block { Type = BlockType.Code, Label = pendingLabel };
                    blocks.Add(block);
                    current = block;
                    pendingLabel = null;
                }
                current.Lines.Add(line);
                continue;
            }

            if (exampleCode)
            {
                Close();
            }

            if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("• "))
            {
                Start(BlockType.ListItem).Lines.Add(trimmed.Substring(2).Trim());
                continue;
            }

            if (current != null && !current.Closed && current.Type != BlockType.Code)
            {
                // continuation of a paragraph, note or list item
                current.Lines.Add(trimmed);
                if (EndsSentence(trimmed))
                {
                    Close();
                }
                continue;
            }

            AddText(trimmed);
        }

        return RenderBlocks(blocks);
    }

    static private string RenderBlocks(List<Block> blocks)
    {
        var sb = new StringBuilder();
        bool inList = false;

        foreach (var block in blocks)
        {
            if (block.Lines.Count == 0 && block.Type != BlockType.Note && block.Type != BlockType.Warning)
            {
                continue;
            }

            if (block.Type == BlockType.ListItem)
            {
                if (!inList)
                {
                    sb.Append("<ul class='help-list'>");
                    inList = true;
                }
                sb.Append($"<li>{InlineHtml(String.Join(" ", block.Lines))}</li>");
                continue;
            }

            if (inList)
            {
                sb.Append("</ul>");
                inList = false;
            }

            switch (block.Type)
            {
                case BlockType.Paragraph:
                    sb.Append($"<p>{InlineHtml(String.Join(" ", block.Lines))}</p>");
                    break;

                case BlockType.Note:
                case BlockType.Warning:
                    if (block.Lines.Count == 0) break;
                    sb.Append($"<div class='help-note{(block.Type == BlockType.Warning ? " help-note-warning" : "")}'>");
                    sb.Append($"<span class='help-note-label'>{Encode(block.Label)}</span>");
                    sb.Append($"<span>{InlineHtml(String.Join(" ", block.Lines))}</span>");
                    sb.Append("</div>");
                    break;

                case BlockType.Code:
                    AppendCodeBlock(sb, Dedent(block.Lines), block.Label);
                    break;
            }
        }

        if (inList)
        {
            sb.Append("</ul>");
        }

        return sb.ToString();
    }

    static private string Dedent(List<string> lines)
    {
        var nonEmpty = lines.Where(l => l.Trim().Length > 0).ToList();
        if (nonEmpty.Count == 0)
        {
            return String.Empty;
        }

        int indent = nonEmpty.Min(l => l.Length - l.TrimStart().Length);

        return String.Join("\n", lines.Select(l => l.Length >= indent ? l.Substring(indent) : l.TrimStart())).Trim('\n');
    }

    static private void AppendCodeBlock(StringBuilder sb, string code, string label, string highlightedHtml = null)
    {
        sb.Append("<div class='help-code-block'>");
        sb.Append("<div class='help-code-toolbar'>");
        sb.Append($"<span class='help-code-label'>{Encode(label ?? "")}</span>");
        sb.Append("<button type='button' class='copy-button' title='Copy'>Copy</button>");
        sb.Append("</div>");
        sb.Append($"<pre class='help-code copyable-content'><code>{highlightedHtml ?? Encode(code)}</code></pre>");
        sb.Append("</div>");
    }

    static private string InlineHtml(string text)
    {
        var encoded = Encode(text);

        // `code` => <code>code</code>
        return Regex.Replace(encoded, "`([^`]+)`", "<code>$1</code>");
    }

    static private string Encode(string text) => WebUtility.HtmlEncode(text ?? String.Empty);

    static private string Truncate(string text, int maxLength)
        => text.Length > maxLength ? text.Substring(0, maxLength).TrimEnd() + "…" : text;

    #endregion

    #region Signature / Example

    private string Receiver(MethodHelp method) => method.Target ?? Alias;

    private void RenderSignature(MethodHelp method, StringBuilder sb)
    {
        var html = new StringBuilder();

        html.Append($"<span class='tok-type'>{Encode(TypeName(method.ReturnType))}</span> ");
        html.Append($"<span class='tok-receiver'>{Encode(Receiver(method))}</span>.");
        html.Append($"<span class='tok-method'>{Encode(method.Name)}</span>(");

        for (int i = 0; i < method.Parameters.Count; i++)
        {
            var parameter = method.Parameters[i];

            html.Append("\n    ");
            html.Append($"<span class='tok-type'>{Encode(TypeName(parameter.ParameterType))}</span> ");
            html.Append($"<span class='tok-param'>{Encode(parameter.Name)}</span>");

            if (parameter.HasDefault)
            {
                html.Append($" = {LiteralHtml(parameter, parameter.DefaultValue)}");
            }

            if (i < method.Parameters.Count - 1)
            {
                html.Append(',');
            }
        }

        if (method.Parameters.Count > 0)
        {
            html.Append('\n');
        }

        html.Append(')');

        sb.Append("<pre class='help-code help-signature'><code>");
        sb.Append(html);
        sb.Append("</code></pre>");
    }

    private void RenderExample(MethodHelp method, StringBuilder sb, bool renderDefaults)
    {
        var html = new StringBuilder();
        var receiver = Receiver(method);
        var prefix = $"@{receiver}.{method.Name}(";

        html.Append($"<span class='tok-receiver'>@{Encode(receiver)}</span>.<span class='tok-method'>{Encode(method.Name)}</span>(");

        var parameters = method.Parameters.Where(p => renderDefaults || !p.HasDefault).ToList();

        for (int i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];

            if (i > 0)
            {
                html.Append(",\n");
                html.Append(new string(' ', prefix.Length));
            }

            html.Append($"<span class='tok-param'>{Encode(parameter.Name)}</span>: ");
            html.Append(parameter.HasDefault
                ? LiteralHtml(parameter, parameter.DefaultValue)
                : PlaceholderHtml(parameter));
        }

        html.Append(')');

        AppendCodeBlock(sb, null, null, html.ToString());
    }

    static private string LiteralHtml(MethodHelp.ParameterHelp parameter, object value)
    {
        if (value is null)
        {
            return "<span class='tok-keyword'>null</span>";
        }

        if (value is bool flag)
        {
            return $"<span class='tok-keyword'>{(flag ? "true" : "false")}</span>";
        }

        if (value is string text)
        {
            return $"<span class='tok-string'>\"{Encode(text)}\"</span>";
        }

        if (parameter.ParameterType.IsEnum)
        {
            return $"<span class='tok-literal'>{Encode(parameter.ParameterType.Name)}.{Encode(value.ToString())}</span>";
        }

        return $"<span class='tok-literal'>{Encode(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture))}</span>";
    }

    static private string PlaceholderHtml(MethodHelp.ParameterHelp parameter)
    {
        var type = parameter.ParameterType;

        if (type == typeof(bool)) return "<span class='tok-keyword'>true</span>";
        if (type == typeof(string)) return "<span class='tok-string'>\"...\"</span>";
        if (type == typeof(int) || type == typeof(short) || type == typeof(long)) return "<span class='tok-literal'>1</span>";
        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal)) return "<span class='tok-literal'>1.0</span>";
        if (type.IsEnum) return $"<span class='tok-literal'>{Encode(type.Name)}.{Encode(Enum.GetNames(type).FirstOrDefault())}</span>";

        switch (parameter.Name.ToLowerInvariant())
        {
            case "record": return "<span class='tok-receiver'>record</span>";
            case "records": return "<span class='tok-receiver'>Model.Records</span>";
            default: return "<span class='tok-keyword'>...</span>";
        }
    }

    static private string DefaultValueText(MethodHelp.ParameterHelp parameter)
        => parameter.DefaultValue switch
        {
            null => "null",
            bool flag => flag ? "true" : "false",
            string text => $"\"{text}\"",
            _ => Convert.ToString(parameter.DefaultValue, System.Globalization.CultureInfo.InvariantCulture)
        };

    static public string TypeName(Type type)
    {
        if (type is null || type == typeof(void)) return "void";
        if (type == typeof(object)) return "object";
        if (type == typeof(string)) return "string";
        if (type == typeof(int)) return "int";
        if (type == typeof(short)) return "short";
        if (type == typeof(long)) return "long";
        if (type == typeof(double)) return "double";
        if (type == typeof(float)) return "float";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(bool)) return "bool";
        if (type.IsArray) return TypeName(type.GetElementType()) + "[]";

        var nullable = Nullable.GetUnderlyingType(type);
        if (nullable != null) return TypeName(nullable) + "?";

        if (type.IsGenericType)
        {
            return type.Name.Split('`')[0] + "<" + String.Join(", ", type.GetGenericArguments().Select(TypeName)) + ">";
        }

        return type.Name;
    }

    #endregion

    #region Texts

    internal sealed class HelpTexts
    {
        public string Signature, Example, Description, Parameters, Optional, Overload, NotFound;

        static private readonly HelpTexts En = new HelpTexts
        {
            Signature = "Signature",
            Example = "Example",
            Description = "Description",
            Parameters = "Parameters",
            Optional = "optional",
            Overload = "Overload",
            NotFound = "No documentation found for this function."
        };

        static private readonly HelpTexts De = new HelpTexts
        {
            Signature = "Signatur",
            Example = "Beispiel",
            Description = "Beschreibung",
            Parameters = "Parameter",
            Optional = "optional",
            Overload = "Überladung",
            NotFound = "Für diese Funktion wurde keine Dokumentation gefunden."
        };

        static public HelpTexts For(string language)
            => "de".Equals(language, StringComparison.OrdinalIgnoreCase) ? De : En;
    }

    #endregion
}
