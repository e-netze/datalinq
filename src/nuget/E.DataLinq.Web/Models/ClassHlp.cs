using E.DataLinq.Core;
using E.DataLinq.Core.Reflection;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Razor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;

namespace E.DataLinq.Web.Models;

public class ClassHelp
{
    public ClassHelp()
    {
        Methods = new List<MethodHelp>();
        ExtensionMethods = new List<MethodHelp>();
    }

    public string Name { get; set; }

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

        public string Description { get; set; }

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

    public string ToHtmlString(string methodName)
    {
        StringBuilder sb = new StringBuilder();

        if (String.IsNullOrWhiteSpace(methodName))
        {
            if (!String.IsNullOrWhiteSpace(Description))
            {
                sb.Append("<div>" + Description + "</div>");
            }
        }

        int count = 0;

        var methods = new List<MethodHelp>(this.Methods.Where(m => m.Name == methodName));
        methods.AddRange(this.ExtensionMethods.Where(m => m.Name == methodName));

        foreach (var method in methods)
        {
            if (!String.IsNullOrWhiteSpace(methodName) && methodName != method.Name)
            {
                continue;
            }

            if (count == 1 && !String.IsNullOrWhiteSpace(methodName))
            {
                sb.Append("<strong>Überladungen</strong>");
            }

            #region Method Definition

            sb.Append("<pre class='method-def'>");
            sb.Append("<span class='keyword'>public&nbsp;</span>");
            sb.Append("<span class='" + (IsKeyword(method.ReturnType) ? "keyword" : "") + "'>" + TypeToString(method.ReturnType) + "&nbsp;</span>");
            sb.Append("<span>" + method.Name + "</span>");
            sb.Append("<span>(</span>");
            if (method.Parameters != null && method.Parameters.Count > 0)
            {
                sb.Append("<br/>");
            }

            bool firstParameter = true;
            foreach (var parameter in method.Parameters)
            {
                if (!firstParameter)
                {
                    sb.Append("<span>,&nbsp;</span><br/>");
                }

                sb.Append("<span>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;</span><span class='" + (IsKeyword(parameter.ParameterType) ? "keyword" : "") + "'>" + TypeToString(parameter.ParameterType) + "</span>");
                sb.Append("<span>&nbsp;</span>");
                sb.Append("<span>" + parameter.Name + "</span>");

                if (parameter.HasDefault)
                {
                    sb.Append("<span>&nbsp;=&nbsp</span>");
                    if (parameter.ParameterType == typeof(string))
                    {
                        sb.Append("<span class='literal'>\"</span>");
                    }

                    if (parameter.DefaultValue == null)
                    {
                        sb.Append("<span class='keyword'>null</span>");
                    }
                    else if (parameter.ParameterType == typeof(bool))
                    {
                        sb.Append("<span class='keyword'>" + parameter.DefaultValue.ToString().ToLower() + "</span>");
                    }
                    else
                    {
                        sb.Append("<span class='literal'>" + parameter.DefaultValue.ToString() + "</span>");
                    }

                    if (parameter.ParameterType == typeof(string))
                    {
                        sb.Append("<span class='literal'>\"</span>");
                    }
                }

                firstParameter = false;
            }

            if (method.Parameters != null && method.Parameters.Count > 0)
            {
                sb.Append("<br/>");
            }

            sb.Append("<span>)</span>");
            sb.Append("</pre>");

            #endregion

            #region Method Example


            sb.Append("<strong>Example:</strong>");

            RenderExample(method, sb, false);
            if (method.Parameters.Where(p => p.HasDefault).Any())
            {
                RenderExample(method, sb, true);
            }

            #endregion

            #region Description 

            if (!String.IsNullOrWhiteSpace(method.Description))
            {
                sb.Append("<div>" + ParseDescription(method.Description) + "</div><br/>");
            }

            if (method.Parameters.Count > 0)
            {
                foreach (var parameter in method.Parameters)
                {
                    if (!String.IsNullOrWhiteSpace(parameter.Description))
                    {
                        sb.Append("<div><div class='parameter-name'>" + parameter.Name + ":</div>");
                        sb.Append(ParseDescription(parameter.Description) + "</div>");
                        sb.Append("<br/>");
                    }

                }
                sb.Append("<br/><br/>");
            }


            #endregion

            count++;
        }

        return sb.ToString();
    }

    #region Helpers

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

        //if (type == typeof(RawString))
        //    return "RawString";
        if (type == typeof(object))  // eigentlich ist RawString jetzt "object" -> Die Methode wird meiner Meinung nur in der Datalinq Hilfe verwendet um den Methodenheader zu erstellen...
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
            return genericTypeName.Replace("<", "&lt;").Replace(">", "&gt;");
        }
        return type.Name;
    }

    private bool IsKeyword(Type type)
    {
        return TypeToString(type) == TypeToString(type).ToLower();
    }

    private string ParseDescription(string description)
    {
        return description.Replace("((", "<pre class='descr-code'>").Replace("))", "</pre>").Replace("\n", "<br/>").Replace("\r", "");
    }

    private void RenderExample(MethodHelp method, StringBuilder sb, bool renderDefaults)
    {
        sb.Append("<pre class='method-def'>");

        sb.Append("<div class='copyable-content'>");

        sb.Append($"<span class='keyword'>@DLH.{method.Name}</span>");
        sb.Append("<span class='literal'>(</span>");

        bool firstParameter = true;
        foreach (var parameter in method.Parameters)
        {
            if (parameter.HasDefault && renderDefaults == false)  // do not show default variables
            {
                continue;
            }

            if (!firstParameter)
            {
                sb.Append("<span>,&nbsp;</span><br/>");
                sb.Append($"<span>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;");
                for (int i = 0; i < method.Name.Length; i++)
                {
                    sb.Append($"&nbsp;");
                }
                sb.Append("</span>");
            }

            sb.Append($"<span>{parameter.Name}</span>");
            sb.Append("<span>:</span>");

            sb.Append("<span>&nbsp;</span>");


            if (parameter.ParameterType == typeof(string))
            {
                sb.Append("<span class='literal'>\"</span>");
            }

            if (parameter.HasDefault)
            {
                if (parameter.DefaultValue == null)
                {
                    sb.Append("<span class='keyword'>null</span>");
                }
                else if (parameter.ParameterType == typeof(bool))
                {
                    sb.Append("<span class='keyword'>" + parameter.DefaultValue.ToString().ToLower() + "</span>");
                }
                else
                {
                    sb.Append("<span class='literal'>" + parameter.DefaultValue.ToString() + "</span>");
                }
            }
            else
            {
                if (parameter.ParameterType == typeof(bool))
                {
                    sb.Append("<span class='keyword'>" + true.ToString().ToLower() + "</span>");
                }
                else if (parameter.ParameterType == typeof(string))
                {
                    sb.Append("<span class='keyword'>...</span>");
                }
                else if (parameter.ParameterType == typeof(int) || parameter.ParameterType == typeof(short) || parameter.ParameterType == typeof(long))
                {
                    sb.Append("<span class='keyword'>1</span>");
                }
                else if (parameter.ParameterType == typeof(double) || parameter.ParameterType == typeof(float) || parameter.ParameterType == typeof(decimal))
                {
                    sb.Append("<span class='keyword'>1.0</span>");
                }
                else if (parameter.ParameterType.IsEnum)
                {
                    sb.Append($"<span class='keyword'>{parameter.ParameterType.ToString().Split('.').Last()}.{Enum.GetNames(parameter.ParameterType).FirstOrDefault()}</span>");
                }
                else
                {
                    switch (parameter.Name.ToLower())
                    {
                        case "record":
                            sb.Append("<span class='keyword'>record</span>");
                            break;
                        case "records":
                            sb.Append("<span class='keyword'>Model.Records</span>");
                            break;
                        default:
                            sb.Append("<span class='keyword'>...</span>");
                            break;
                    }
                }
            }

            if (parameter.ParameterType == typeof(string))
            {
                sb.Append("<span class='literal'>\"</span>");
            }

            firstParameter = false;
        }

        sb.Append("<span class='literal'>)</span>");

        sb.Append("<button class='copy-button'>Copy</button>");

        sb.Append("</div>");

        sb.Append("</pre>");
    }

    #endregion

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
    static public ClassHelp FromTypeUseXmlDocumentation(Type type, string languageCode)
    {
        ClassHelp classHelp = new ClassHelp()
        {
            Name = type.Name
        };

        var xmlFilePath = $"{System.IO.Path.ChangeExtension(type.Assembly.Location, ".XML")}";
        if (!System.IO.File.Exists(xmlFilePath))
        {
            return classHelp;
        }

        var xdoc = XDocument.Load(xmlFilePath);

        // read the comment summary for this type
        var typeSummary = GetXmlDocumentation(xdoc, type, languageCode);

        classHelp.Description = typeSummary;

        int count = 0;

        foreach (var methodInfo in type.GetMethods())
        {
            if (!methodInfo.IsPublic)
            {
                break;
            }

            // read the comment summery and params for this methodInfo
            var methodSummary = GetXmlDocumentation(xdoc, methodInfo, count, languageCode);

            var methodHelp = new ClassHelp.MethodHelp()
            {
                Name = methodInfo.Name,
                ReturnType = methodInfo.ReturnType,
                Description = methodSummary
            };
            classHelp.Methods.Add(methodHelp);

            foreach (var parameterInfo in methodInfo.GetParameters())
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

        AddExtensionMethodsUseAttributes(classHelp);

        return classHelp;
    }

    #region Static Helpers

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
        var memberName = $"M:{methodInfo.DeclaringType.FullName}.{methodInfo.Name}";
        var member = xdoc
            .Descendants("member")
            .Skip(count)
            .FirstOrDefault(m => m.Attribute("name")?.Value.StartsWith(memberName) == true);

        return member?.Element("summary")?.Value.ExtractLanguage(languageCode);
    }
    private static string GetXmlDocumentation(XDocument xdoc, ParameterInfo parameterInfo, int count, string languageCode)
    {
        var memberName = $"M:{parameterInfo.Member.DeclaringType.FullName}.{parameterInfo.Member.Name}";
        var member = xdoc
            .Descendants("member")
            .Skip(1).Skip(count)
            .FirstOrDefault(m => m.Attribute("name")?.Value.StartsWith(memberName) == true);

        var param = member?
            .Elements("param")
            .FirstOrDefault(p => p.Attribute("name")?.Value == parameterInfo.Name);

        return param?.Value.Trim().ExtractLanguage(languageCode);
    }

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
