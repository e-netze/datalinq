using System.Collections.Specialized;
using System.Text.RegularExpressions;

namespace E.DataLinq.Web.Extensions;

internal static class StringExtensions
{
    static public NameValueCollection ToKeyValues(this string str, string defaultParameterName = "key")
    {
        var keyValues = new NameValueCollection();
        if (!str.Contains(";"))
        {
            keyValues[defaultParameterName] = str;
        }
        foreach (var keyValue in str.Split(';'))
        {
            if (keyValue.Contains("="))
            {
                var key = keyValue.Substring(0, keyValue.IndexOf("="));
                var value = keyValue.Substring(keyValue.IndexOf("=") + 1);

                keyValues[key] = value;
            }
            else
            {
                keyValues[defaultParameterName] = keyValue.Trim();
            }
        }

        return keyValues;
    }

    static public string[] KeyParameters(this string commandLine, string startingBracket = "[", string endingBracket = "]")
    {
        int pos1 = 0, pos2;
        pos1 = commandLine.IndexOf(startingBracket);
        string parameters = "";

        while (pos1 != -1)
        {
            pos2 = commandLine.IndexOf(endingBracket, pos1);
            if (pos2 == -1)
            {
                break;
            }

            if (parameters != "")
            {
                parameters += ";";
            }

            parameters += commandLine.Substring(pos1 + startingBracket.Length, pos2 - pos1 - endingBracket.Length);
            pos1 = commandLine.IndexOf(startingBracket, pos2);
        }
        if (parameters != "")
        {
            return parameters.Split(';');
        }
        else
        {
            return null;
        }
    }

    static public string ToRazorAssemblyFilename(this string id)
        => $"razor_{id}.dll";

    public static string CleanRazorString(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Entferne Razor-Block-Kommentare (@* ... *@)
        input = Regex.Replace(input, @"@\*.*?\*@", "", RegexOptions.Singleline);

        // Entferne C-Style-Block-Kommentare (/* ... */)
        input = Regex.Replace(input, @"/\*.*?\*/", "", RegexOptions.Singleline);

        // Entferne einzeilige Kommentare (// ...)
        input = Regex.Replace(input, @"//.*", "");

        // Entferne überflüssige Leerzeilen
        input = Regex.Replace(input, @"(^\s*\n)|(^\s*$)", "", RegexOptions.Multiline);

        // Entferne doppelte Leerzeichen
        input = Regex.Replace(input, @"\s+", " ").Trim();

        // Setze Leerzeichen korrekt für Klammern und Semikolons
        input = Regex.Replace(input, @"\s*{\s*", " {\n");
        input = Regex.Replace(input, @"\s*}\s*", "\n}\n");
        input = Regex.Replace(input, @"\s*;\s*", ";\n");
        input = Regex.Replace(input, @"\s*\(\s*", "(");
        input = Regex.Replace(input, @"\s*\)\s*", ")");

        // Entferne Leerzeichen um Punkte für Methodenzugriffe
        input = Regex.Replace(input, @"\s*\.\s*", ".");

        // Entferne Leerzeichen um @ für @using, @inject directiven
        input = Regex.Replace(input, @"\s*\@\s*", "@");

        return input;
    }

}