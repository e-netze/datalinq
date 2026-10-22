using E.DataLinq.Web.Models;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace E.DataLinq.Web.Mcp;

// Builds compact, agent-friendly helper references (DLH, PDF, SECURITY, Model.Records) from the XML documentation
// that is also used by the DataLinq Code help. Agents must only use methods listed here.
static public class DataLinqMcpHelperReference
{
    static private readonly ConcurrentDictionary<string, ClassHelp> Cache = new();

    static public ClassHelp Get(Type type, string alias, bool includeExtensionMethods = false, bool recordsExtensionMethods = false)
        => Cache.GetOrAdd($"{type.FullName}|{alias}|{includeExtensionMethods}|{recordsExtensionMethods}",
            _ => ClassHelp.FromTypeUseXmlDocumentation(type, "en", alias,
                                                       includeExtensionMethods: includeExtensionMethods,
                                                       recordsExtensionMethods: recordsExtensionMethods));

    static public object ToResult(ClassHelp help, string method = null, string search = null, bool details = false)
    {
        var methods = help.Methods.Concat(help.ExtensionMethods)
            .Where(m => String.IsNullOrWhiteSpace(method) || m.Name.Equals(method, StringComparison.OrdinalIgnoreCase))
            .Where(m => String.IsNullOrWhiteSpace(search) ||
                        m.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        (m.Description ?? "").Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // without filter: names + one-line description only (cheap); with filter or details: full signatures
        var full = details || !String.IsNullOrWhiteSpace(method) || !String.IsNullOrWhiteSpace(search);

        return new
        {
            alias = help.Alias,
            description = full ? help.Description : FirstSentence(help.Description),
            count = methods.Length,
            methods = full
                ? methods.Select(m => (object)new
                {
                    name = m.Name,
                    signature = Signature(help.Alias, m),
                    returns = TypeName(m.ReturnType),
                    description = m.Description,
                    parameters = m.Parameters.Select(p => new
                    {
                        name = p.Name,
                        type = TypeName(p.ParameterType),
                        optional = p.HasDefault,
                        @default = p.HasDefault ? p.DefaultValue?.ToString() ?? "null" : null,
                        description = p.Description
                    })
                }).ToArray()
                : methods.GroupBy(m => m.Name)
                         .Select(g => (object)new { name = g.Key, overloads = g.Count(), summary = FirstSentence(g.First().Description) })
                         .ToArray(),
            note = full
                ? "Use only these methods/parameters. Async methods (Task) must be called with @await."
                : "Call again with method='<name>' (or search='<text>') for full signatures and parameter descriptions."
        };
    }

    static public string Signature(string alias, ClassHelp.MethodHelp m)
        => $"{TypeName(m.ReturnType)} {alias}.{m.Name}(" +
           String.Join(", ", m.Parameters.Select(p =>
               $"{TypeName(p.ParameterType)} {p.Name}" + (p.HasDefault ? $" = {DefaultText(p)}" : ""))) + ")";

    static private string DefaultText(ClassHelp.MethodHelp.ParameterHelp p)
        => p.DefaultValue switch
        {
            null => "null",
            string s => $"\"{s}\"",
            bool b => b ? "true" : "false",
            _ => p.DefaultValue.ToString()
        };

    static public string TypeName(Type type)
    {
        if (type is null) return "void";
        if (type == typeof(void)) return "void";
        if (type == typeof(string)) return "string";
        if (type == typeof(object)) return "object";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(int)) return "int";
        if (type == typeof(double)) return "double";
        if (type == typeof(float)) return "float";
        if (type == typeof(long)) return "long";
        if (type.IsArray) return TypeName(type.GetElementType()) + "[]";

        var nullable = Nullable.GetUnderlyingType(type);
        if (nullable != null) return TypeName(nullable) + "?";

        if (type.IsGenericType)
        {
            var name = type.Name;
            var tick = name.IndexOf('`');
            if (tick > 0) name = name.Substring(0, tick);
            return $"{name}<{String.Join(", ", type.GetGenericArguments().Select(TypeName))}>";
        }

        return type.Name;
    }

    static private string FirstSentence(string text)
    {
        if (String.IsNullOrWhiteSpace(text)) return String.Empty;
        text = text.Trim();
        var dot = text.IndexOf(". ", StringComparison.Ordinal);
        var s = dot > 0 ? text.Substring(0, dot + 1) : text;
        return s.Length > 160 ? s.Substring(0, 160) + "..." : s;
    }
}
