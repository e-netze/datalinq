using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Services.Abstraction;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;

public class MonacoSnippetService : IMonacoSnippetService
{
    private readonly IReadOnlyDictionary<string, Type> _targetTypes;

    public MonacoSnippetService(IReadOnlyDictionary<string, Type> targetTypes)
    {
        _targetTypes = targetTypes ?? throw new ArgumentNullException(nameof(targetTypes));
    }

    public MonacoSnippetService(Type targetType)
        : this(new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) { ["dlh"] = targetType })
    {
    }

    public string BuildSnippetJson(string lang, string helper = "dlh")
    {
        if (!_targetTypes.TryGetValue(helper ?? "dlh", out var targetType))
        {
            targetType = _targetTypes.Values.FirstOrDefault();
        }

        if (targetType == null)
        {
            return "[]";
        }

        var snippets = new List<object>();

        var methods = targetType
                            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                            .Where(m => m.GetCustomAttribute<ExcludeFromSnippetsAttribute>() == null)
                            .Where(m => IsRecordsExtensionTarget(helper) == false || IsRecordsExtensionMethod(m))
                            .ToArray();

        var currentMethod = "";
        var skipper = 0;

        foreach (var method in methods)
        {
            if (currentMethod.Equals(method.Name))
                skipper = 1;
            else
            {
                skipper = 0;
                currentMethod = method.Name;
            }

            var methodDescription = GetDescriptionFromXML(targetType, lang, method, skipper);

            var parameters = IsExtensionMethod(method)
                ? method.GetParameters().Skip(1).ToArray()
                : method.GetParameters();

            var genericArguments = method.IsGenericMethodDefinition
                ? method.GetGenericArguments()
                : Array.Empty<Type>();

            var label = $"{method.Name}{(genericArguments.Length > 0 ? $"<{String.Join(", ", genericArguments.Select(g => g.Name))}>" : "")}";

            var genericSnippet = genericArguments.Length > 0
                ? $"<{String.Join(", ", genericArguments.Select((g, i) => $"${{{i + 1}:{g.Name}}}"))}>"
                : "";

            var tabIndex = genericArguments.Length;

            var insertTextLines = new List<string>();
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                var comma = (i < parameters.Length - 1) ? "," : "";
                var value = BuildValueSnippet(p.ParameterType, ref tabIndex, "    ", 0);
                insertTextLines.Add($"    {value}{comma} //{p.Name}");
            }

            var insertText = new StringBuilder();
            insertText.AppendLine($"{method.Name}{genericSnippet}(");
            insertText.AppendLine(string.Join(",\n", insertTextLines));
            insertText.Append(")");

            var snippet = new
            {
                label,
                kind = 3,
                insertText = insertText.ToString(),
                insertTextRules = 4,
                documentation = $"{label}({string.Join(", ", parameters.Select(p => TypeToString(p.ParameterType)))})\n\n" + methodDescription
            };

            snippets.Add(snippet);
        }

        return JsonConvert.SerializeObject(snippets, Formatting.Indented);
    }

    private const string RecordsHelperKey = "records";

    static private bool IsRecordsExtensionTarget(string helper)
        => RecordsHelperKey.Equals(helper, StringComparison.OrdinalIgnoreCase);

    static private bool IsExtensionMethod(MethodInfo method)
        => method.IsStatic
        && method.IsDefined(typeof(ExtensionAttribute), false)
        && method.GetParameters().Length > 0;

    static private bool IsRecordsExtensionMethod(MethodInfo method)
    {
        if (!IsExtensionMethod(method))
        {
            return false;
        }

        var thisParameterType = method.GetParameters()[0].ParameterType;

        return thisParameterType.IsGenericType
            && thisParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>);
    }

    private string BuildValueSnippet(Type type, ref int tabIndex, string indent, int depth)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying.IsEnum)
        {
            tabIndex++;
            var names = Enum.GetNames(underlying);
            return $"{underlying.Name}.${{{tabIndex}|{string.Join(",", names)}|}}";
        }

        if (depth < 2 && IsComplexType(underlying))
        {
            var properties = underlying
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(pr => pr.CanWrite && pr.GetIndexParameters().Length == 0)
                .ToArray();

            if (properties.Length > 0)
            {
                var sb = new StringBuilder();
                sb.Append($"new {TypeToString(underlying)}");
                sb.Append($"\n{indent}{{");

                for (int i = 0; i < properties.Length; i++)
                {
                    var property = properties[i];
                    var comma = (i < properties.Length - 1) ? "," : "";
                    var value = BuildValueSnippet(property.PropertyType, ref tabIndex, indent + "    ", depth + 1);
                    sb.Append($"\n{indent}    {property.Name} = {value}{comma}");
                }

                sb.Append($"\n{indent}}}");
                return sb.ToString();
            }
        }

        tabIndex++;
        return $"${{{tabIndex}:{TypeToString(type)}}}";
    }

    private static bool IsComplexType(Type type)
    {
        if (type.IsPrimitive || type.IsEnum)
        {
            return false;
        }

        if (type == typeof(string) || type == typeof(decimal) || type == typeof(object)
            || type == typeof(DateTime) || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan) || type == typeof(Guid))
        {
            return false;
        }

        if (type.IsArray || type.IsGenericType || !type.IsClass)
        {
            return false;
        }

        return type.GetConstructor(Type.EmptyTypes) != null;
    }

    private string TypeToString(Type type)
    {

        if (type == typeof(string))
        {
            return "string";
        }

        if (type == typeof(int))
        {
            return "int";
        }

        if (type == typeof(short))
        {
            return "short";
        }

        if (type == typeof(long))
        {
            return "long";
        }

        if (type == typeof(double))
        {
            return "double";
        }

        if (type == typeof(float))
        {
            return "float";
        }

        if (type == typeof(decimal))
        {
            return "decimal";
        }

        if (type == typeof(bool))
        {
            return "bool";
        }

        if (type == typeof(object))
        {
            return "object";
        }

        if (type.IsArray)
        {
            return TypeToString(type.GetElementType()) + "[]";
        }

        if (type.IsGenericType)
        {
            string genericTypeName = type.Name.Split('`')[0] + "<";
            bool first = true;
            foreach (var genericType in type.GetGenericArguments())
            {
                if (!first)
                {
                    genericTypeName += ", ";
                }

                genericTypeName += TypeToString(genericType);
                first = false;
            }
            genericTypeName += ">";
            return genericTypeName;
        }
        return type.Name;
    }

    private static string GetDescriptionFromXML(Type type, string languageCode, MethodInfo methodInfo, int skipper)
    {
        var xmlFilePath = $"{Path.ChangeExtension(type.Assembly.Location, ".XML")}";
        if (!File.Exists(xmlFilePath))
        {
            return "";
        }

        var xdoc = XDocument.Load(xmlFilePath);

        var memberName = $"M:{methodInfo.DeclaringType.FullName}.{methodInfo.Name}";

        var member = xdoc
            .Descendants("member")
            .Where(m => m.Attribute("name")?.Value.StartsWith(memberName) == true)
            .Skip(skipper).FirstOrDefault();

        return member?.Element("summary")?.Value.ExtractLanguage(languageCode);
    }
}
