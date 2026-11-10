using E.DataLinq.Core;
using E.DataLinq.Core.Reflection;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Razor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;

namespace E.DataLinq.Web.Models;

public partial class ClassHelp
{
    public ClassHelp()
    {
        Methods = new List<MethodHelp>();
        ExtensionMethods = new List<MethodHelp>();
    }

    public string Name { get; set; }

    public string Alias { get; set; } = "DLH";

    public string Description { get; set; }

    public List<MethodHelp> Methods { get; set; }

    public List<MethodHelp> ExtensionMethods { get; set; }

    public List<string> MethodNames
    {
        get
        {
            List<string> ret = new List<string>();
            foreach (var method in this.Methods)
            {
                if (!ret.Contains(method.Name))
                {
                    ret.Add(method.Name);
                }
            }
            return ret;
        }
    }

    public List<string> ExtensionMethodNames
    {
        get
        {
            List<string> ret = new List<string>();

            foreach (var method in this.ExtensionMethods)
            {
                if (!ret.Contains(method.Name))
                {
                    ret.Add(method.Name);
                }
            }

            return ret;
        }
    }

    public class MethodHelp
    {
        public string Name { get; set; }

        /// <summary>
        /// Optional receiver shown in signatures instead of the class alias (e.g. "record" for single record extension methods).
        /// </summary>
        public string Target { get; set; }

        public string Description { get; set; }

        /// <summary>
        /// One sentence from the &lt;short&gt; doc tag; used in overviews instead of the full description.
        /// </summary>
        public string ShortDescription { get; set; }

        public MethodHelp()
        {
            this.Parameters = new List<ParameterHelp>();
        }
        public List<ParameterHelp> Parameters { get; set; }
        public Type ReturnType { get; set; }

        public class ParameterHelp
        {
            public Type ParameterType { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public bool HasDefault { get; set; }
            public object DefaultValue { get; set; }
        }
    }

    #region Static Members

    static public ClassHelp FromTypeUseAttributes(Type type)
    {
        ClassHelp classHelp = new ClassHelp()
        {
            Name = type.Name
        };

        var descriptionAttribute = typeof(DataLinqHelper).GetCustomAttribute<HelpDescriptionAttribute>();
        if (descriptionAttribute != null)
        {
            classHelp.Description = descriptionAttribute.Description;
        }

        foreach (var methodInfo in type.GetMethods())
        {
            if (!methodInfo.IsPublic)
            {
                break;
            }

            descriptionAttribute = (HelpDescriptionAttribute)methodInfo.GetCustomAttributes(typeof(HelpDescriptionAttribute), false).FirstOrDefault();

            var methodHelp = new ClassHelp.MethodHelp()
            {
                Name = methodInfo.Name,
                ReturnType = methodInfo.ReturnType,
                Description = descriptionAttribute?.Description
            };
            classHelp.Methods.Add(methodHelp);

            foreach (var parameterInfo in methodInfo.GetParameters())
            {
                descriptionAttribute = (HelpDescriptionAttribute)parameterInfo.GetCustomAttributes(typeof(HelpDescriptionAttribute), false).FirstOrDefault();

                methodHelp.Parameters.Add(new ClassHelp.MethodHelp.ParameterHelp()
                {
                    Name = parameterInfo.Name,
                    ParameterType = parameterInfo.ParameterType,
                    HasDefault = parameterInfo.HasDefaultValue,
                    DefaultValue = parameterInfo.DefaultValue,
                    Description = descriptionAttribute?.Description
                });
            }
        }

        AddExtensionMethodsUseAttributes(classHelp);

        return classHelp;
    }

    static public ClassHelp FromTypeUseXmlDocumentation(Type type,
                                                        string languageCode,
                                                        string alias = "DLH",
                                                        bool includeExtensionMethods = true,
                                                        bool recordsExtensionMethods = false)
    {
        ClassHelp classHelp = new ClassHelp()
        {
            Name = type.Name,
            Alias = alias
        };

        XDocument xdoc = null;
        var xmlFilePath = $"{System.IO.Path.ChangeExtension(type.Assembly.Location, "_.xml")}";
        if (System.IO.File.Exists(xmlFilePath))
        {
            xdoc = XDocument.Load(xmlFilePath);
        }
        else
        {
            var generatedXmlPath = System.IO.Path.ChangeExtension(type.Assembly.Location, ".xml");
            if (System.IO.File.Exists(generatedXmlPath))
            {
                xdoc = XDocument.Load(generatedXmlPath);
            }
            else if (E.DataLinq.Web.Properties.Resources.e_datalinq_web_xml?.Any() == true)
            {
                xdoc = XDocument.Parse(
                    System.Text.Encoding.UTF8.GetString(
                        E.DataLinq.Web.Properties.Resources.e_datalinq_web_xml));
            }
        }

        if (xdoc == null)
        {
            return classHelp;
        }

        // read the comment summary for this type
        var typeSummary = GetXmlDocumentation(xdoc, type, languageCode);

        classHelp.Description = typeSummary;

        int count = 0;

        foreach (var methodInfo in type.GetMethods())
        {
            if (!methodInfo.IsPublic)
            {
                continue;
            }

            if (recordsExtensionMethods && !IsRecordsExtensionMethod(methodInfo))
            {
                continue;
            }

            // read the comment summery and params for this methodInfo
            var methodSummary = GetXmlDocumentation(xdoc, methodInfo, count, languageCode);

            var methodHelp = new ClassHelp.MethodHelp()
            {
                Name = methodInfo.Name,
                Target = !recordsExtensionMethods
                    ? null
                    : methodInfo.GetParameters()[0].ParameterType == typeof(IDictionary<string, object>)
                        ? "record"
                        : methodInfo.GetParameters()[0].ParameterType == typeof(KeyValuePair<string, object>)
                            ? "entry"
                            : null,
                ReturnType = methodInfo.ReturnType,
                Description = methodSummary,
                ShortDescription = GetXmlShortDocumentation(xdoc, methodInfo, languageCode)
            };

            if (recordsExtensionMethods)
            {
                classHelp.ExtensionMethods.Add(methodHelp);
            }
            else
            {
                classHelp.Methods.Add(methodHelp);
            }

            // skip the first parameter of extension methods => the "this" parameter!
            var parameterInfos = recordsExtensionMethods
                ? methodInfo.GetParameters().Skip(1)
                : methodInfo.GetParameters();

            foreach (var parameterInfo in parameterInfos)
            {
                var parameterSummary = GetXmlDocumentation(xdoc, parameterInfo, count, languageCode);
                methodHelp.Parameters.Add(new ClassHelp.MethodHelp.ParameterHelp()
                {
                    Name = parameterInfo.Name,
                    ParameterType = parameterInfo.ParameterType,
                    HasDefault = parameterInfo.HasDefaultValue,
                    DefaultValue = parameterInfo.DefaultValue,
                    Description = parameterSummary
                });
            }

            count++;
        }

        if (includeExtensionMethods)
        {
            AddExtensionMethodsUseAttributes(classHelp);
        }

        return classHelp;
    }

    #region Static Helpers

    static private bool IsRecordsExtensionMethod(MethodInfo methodInfo)
    {
        if (!methodInfo.IsStatic
            || !methodInfo.IsDefined(typeof(ExtensionAttribute), false)
            || methodInfo.GetParameters().Length == 0)
        {
            return false;
        }

        var thisParameterType = methodInfo.GetParameters()[0].ParameterType;

        if (thisParameterType.IsGenericType
            && thisParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return true;
        }

        if (thisParameterType == typeof(KeyValuePair<string, object>))
        {
            return true;
        }

        // single record methods (IDictionary<string, object>) are only listed,
        // if there is no Model.Records (IEnumerable) overload with the same name, e.g. JsonParse
        return thisParameterType == typeof(IDictionary<string, object>)
            && !methodInfo.DeclaringType
                          .GetMethods(BindingFlags.Static | BindingFlags.Public)
                          .Any(m => m.Name == methodInfo.Name
                                 && m.IsDefined(typeof(ExtensionAttribute), false)
                                 && m.GetParameters().Length > 0
                                 && m.GetParameters()[0].ParameterType.IsGenericType
                                 && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }

    private const string NotCommented = "Sorry, documentation missing!";

    private static string GetXmlDocumentation(XDocument xdoc, Type type, string languageCode)
    {
        var memberName = $"T:{type.FullName}";
        var member = xdoc
            .Descendants("member")
            .FirstOrDefault(m => m.Attribute("name")?.Value == memberName);

        return member?.Element("summary")?.Value.ExtractLanguage(languageCode);
    }
    private static string GetXmlDocumentation(XDocument xdoc, MethodInfo methodInfo, int count, string languageCode)
    {
        var member = FindMethodMember(xdoc, methodInfo);

        if (member is null)
        {
            var memberName = $"M:{methodInfo.DeclaringType.FullName}.{methodInfo.Name}";
            member = xdoc
                .Descendants("member")
                .Skip(count)
                .FirstOrDefault(m => m.Attribute("name")?.Value.StartsWith(memberName) == true);
        }

        return member?.Element("summary")?.Value.ExtractLanguage(languageCode);
    }
    private static string GetXmlDocumentation(XDocument xdoc, ParameterInfo parameterInfo, int count, string languageCode)
    {
        var member = parameterInfo.Member is MethodInfo methodInfo
            ? FindMethodMember(xdoc, methodInfo)
            : null;

        if (member is null)
        {
            var memberName = $"M:{parameterInfo.Member.DeclaringType.FullName}.{parameterInfo.Member.Name}";
            member = xdoc
                .Descendants("member")
                .Skip(1).Skip(count)
                .FirstOrDefault(m => m.Attribute("name")?.Value.StartsWith(memberName) == true);
        }

        var param = member?
            .Elements("param")
            .FirstOrDefault(p => p.Attribute("name")?.Value == parameterInfo.Name);

        return param?.Value.Trim().ExtractLanguage(languageCode);
    }

    private static string GetXmlShortDocumentation(XDocument xdoc, MethodInfo methodInfo, string languageCode)
    {
        var text = FindMethodMember(xdoc, methodInfo)?
            .Element("short")?
            .Value
            .ExtractLanguage(languageCode);

        return String.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    #region Xml Documentation Ids

    private static XElement FindMethodMember(XDocument xdoc, MethodInfo methodInfo)
    {
        string memberId;

        try
        {
            memberId = XmlDocumentationId(methodInfo);
        }
        catch
        {
            return null;
        }

        return xdoc
            .Descendants("member")
            .FirstOrDefault(m => m.Attribute("name")?.Value == memberId);
    }

    private static string XmlDocumentationId(MethodInfo methodInfo)
    {
        var sb = new StringBuilder();

        sb.Append("M:");
        sb.Append(XmlDocumentationTypeName(methodInfo.DeclaringType));
        sb.Append('.');
        sb.Append(methodInfo.Name);

        if (methodInfo.IsGenericMethod)
        {
            sb.Append("``");
            sb.Append(methodInfo.GetGenericArguments().Length);
        }

        var parameters = methodInfo.GetParameters();
        if (parameters.Length > 0)
        {
            sb.Append('(');
            sb.Append(String.Join(",", parameters.Select(p => XmlDocumentationTypeName(p.ParameterType))));
            sb.Append(')');
        }

        return sb.ToString();
    }

    private static string XmlDocumentationTypeName(Type type)
    {
        if (type.IsGenericParameter)
        {
            return (type.DeclaringMethod is not null ? "``" : "`") + type.GenericParameterPosition;
        }

        if (type.IsByRef)
        {
            return XmlDocumentationTypeName(type.GetElementType()) + "@";
        }

        if (type.IsArray)
        {
            return XmlDocumentationTypeName(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        }

        var name = type.IsNested
            ? XmlDocumentationTypeName(type.DeclaringType) + "." + type.Name
            : (String.IsNullOrEmpty(type.Namespace) ? "" : type.Namespace + ".") + type.Name;

        if (type.IsGenericType)
        {
            var tick = name.IndexOf('`');
            if (tick > 0)
            {
                name = name.Substring(0, tick);
            }

            if (!type.IsGenericTypeDefinition)
            {
                name += "{" + String.Join(",", type.GetGenericArguments().Select(XmlDocumentationTypeName)) + "}";
            }
        }

        return name;
    }

    #endregion

    private static string ExtractLanguageDocumentation(string xmlDoc, string languageCode)
    {
        if (string.IsNullOrEmpty(xmlDoc))
        {
            return string.Empty;
        }

        string languageMarker = languageCode + ":";
        int langIndex = xmlDoc.IndexOf(languageMarker, StringComparison.OrdinalIgnoreCase);

        if (langIndex == -1)
        {
            return xmlDoc;
        }

        int nextLangIndex = xmlDoc.Length;
        string[] possibleLanguages = { "de:", "en:" };

        foreach (var lang in possibleLanguages)
        {
            if (lang != languageCode + ":")
            {
                int index = xmlDoc.IndexOf(lang, langIndex + languageMarker.Length, StringComparison.OrdinalIgnoreCase);
                if (index > -1 && index < nextLangIndex)
                {
                    nextLangIndex = index;
                }
            }
        }

        return xmlDoc.Substring(langIndex + languageMarker.Length, nextLangIndex - (langIndex + languageMarker.Length)).Trim();
    }

    static private void AddExtensionMethodsUseAttributes(ClassHelp classHelp)
    {
        #region Extension Methods

        var extendedType = typeof(IDataLinqHelper);
        var assembly = Assembly.GetEntryAssembly();

        var extendedMethods =
            assembly.GetTypes()
                    .Where(t => t.IsSealed && !t.IsGenericType && !t.IsNested)
                    .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public))
                    .Where(m => m.IsDefined(typeof(ExtensionAttribute), false) &&
                                m.GetParameters()[0].ParameterType == extendedType);

        foreach (var methodInfo in extendedMethods)
        {
            var descriptionAttribute = (HelpDescriptionAttribute)methodInfo.GetCustomAttributes(typeof(HelpDescriptionAttribute), false).FirstOrDefault();

            var methodHelp = new ClassHelp.MethodHelp()
            {
                Name = methodInfo.Name,
                ReturnType = methodInfo.ReturnType,
                Description = descriptionAttribute?.Description
            };

            classHelp.ExtensionMethods.Add(methodHelp);

            // Skip first parameter in ExtensionMethods => the "this" Parameter!
            foreach (var parameterInfo in methodInfo.GetParameters().Skip(1))
            {
                descriptionAttribute = (HelpDescriptionAttribute)parameterInfo.GetCustomAttributes(typeof(HelpDescriptionAttribute), false).FirstOrDefault();

                methodHelp.Parameters.Add(new ClassHelp.MethodHelp.ParameterHelp()
                {
                    Name = parameterInfo.Name,
                    ParameterType = parameterInfo.ParameterType,
                    HasDefault = parameterInfo.HasDefaultValue,
                    DefaultValue = parameterInfo.DefaultValue,
                    Description = descriptionAttribute?.Description
                });
            }
        }

        #endregion
    }

    #endregion

    #endregion
}
