using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace E.DataLinq.Web.Extensions;
internal static class RazorStringExtensions
{
    public static Dictionary<string, string> ExtractMarkedBlocks(this string razorContent)
    {
        var result = new Dictionary<string, string>();

        // Regex zum Erfassen von Blöcken zwischen @*### ID *@ und @*###*@
        var pattern = @"@\*##\s*(.*?)\s*\*@([\s\S]*?)(?=@\*--\*@)";

        var matches = Regex.Matches(razorContent, pattern);

        foreach (Match match in matches)
        {
            if (match.Groups.Count == 3)
            {
                var id = match.Groups[1].Value.Trim();
                var block = match.Groups[2].Value.Trim();

                if (!string.IsNullOrWhiteSpace(id))
                {
                    result[id] = block;
                }
            }
        }

        return result;
    }

    public static (string cleanedText, List<string> comments) ExtractAndRemoveHtmlComments(this string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return (input, new List<string>());
        }

        var comments = new List<string>();

        // Regex zum Finden aller HTML-Kommentare
        string pattern = @"<!--([\s\S]*?)-->";

        string cleaned = Regex.Replace(input, pattern, match =>
        {
            if (match.Groups.Count >= 2)
            {
                comments.Add(match.Groups[1].Value); // full comment excl. <!-- and -->
            }
            
            return string.Empty;
        });

        return (cleaned.Trim(), comments);
    }

    public static List<string> ExtractStringLiteralsWithAt(this string input)
    {
        var result = new List<string>();

        // Regex for normal String-Literals ("...") and verbatim Strings (@"...")
        var pattern = @"@?""((?:[^""\\]|\\.)*)""";

        var matches = Regex.Matches(input, pattern);

        foreach (Match match in matches)
        {
            if (match.Success)
            {
                string literal = match.Groups[1].Value;

                // only add, if there is an @ inside
                if (literal.Contains("@"))
                {
                    // Optional: Escape-Zeichen in normalen Strings entschärfen
                    if (!match.Value.StartsWith("@\""))
                    {
                        literal = Regex.Unescape(literal);
                    }

                    result.Add(literal);
                }
            }
        }

        return result;
    }
}
