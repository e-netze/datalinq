using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using E.DataLinq.Core.Reflection;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Models;
using E.DataLinq.Web.Services.Abstraction;
using Newtonsoft.Json;
using RazorEngine.Compilation.ImpromptuInterface.Dynamic;

public class MonacoSnippetService : IMonacoSnippetService
{
    private readonly Type _targetType;

    public MonacoSnippetService(Type targetType)
    {
        _targetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
    }

    public string BuildSnippetJson()
    {
        var snippets = new List<object>();

        var methods = _targetType
                            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                            .Where(m => m.GetCustomAttribute<ExcludeFromSnippetsAttribute>() == null)
                            .ToArray();

        var currentMethod = "";
        var skipper = 0;

        foreach (var method in methods)
        {
            if(currentMethod.Equals(method.Name))
                skipper = 1;
            else
            {
                skipper = 0;
                currentMethod = method.Name;
            }

            var methodDescription = GetDescriptionFromXML(_targetType, "en", method, skipper);

            var parameters = method.GetParameters();

            var insertTextLines = parameters.Select((p, i) =>
            {
                var defaultVal = TypeToString(p.ParameterType);
                var comma = (i < parameters.Length - 1) ? "," : "";
                return $"    ${{{i + 1}:{defaultVal}}}{comma} //{p.Name}";
            }).ToList();

            var insertText = new StringBuilder();
            insertText.AppendLine($"{method.Name}(");
            insertText.AppendLine(string.Join(",\n", insertTextLines));
            insertText.Append(")");

            var snippet = new
            {
                label = method.Name,
                kind = 3,
                insertText = insertText.ToString(),
                insertTextRules = 4,
                documentation = $"{method.Name}({string.Join(", ", method.GetParameters().Select(p => TypeToString(p.ParameterType)))})\n\n"+methodDescription 
            };

            snippets.Add(snippet);
        }

        return JsonConvert.SerializeObject(snippets, Formatting.Indented);
    }

    private string TypeToString(Type type)
    {
        if (type == typeof(object))
        {
            return "object";
        }

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
        var xmlFilePath = $"{System.IO.Path.ChangeExtension(type.Assembly.Location, ".XML")}";
        if (!System.IO.File.Exists(xmlFilePath))
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
