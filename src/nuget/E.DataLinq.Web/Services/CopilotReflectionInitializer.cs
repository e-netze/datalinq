using E.DataLinq.Core;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Razor;
using E.DataLinq.Web.Services.Abstraction;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Moq;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace E.DataLinq.Web.Services;

public class CopilotReflectionInitializer : IHostedService
{
    private readonly Mock<IRazorCompileEngineService> _razorMock;
    private readonly Mock<IDataLinqUser> _uiMock;
    private readonly DataLinqService _currentDatalinqService;
    private readonly Mock<HttpContext> _httpContextMock;
    private static DataLinqHelper _helper;

    public CopilotReflectionInitializer(DataLinqService currentDatalinqService)
    {
        _razorMock = new Mock<IRazorCompileEngineService>();
        _uiMock = new Mock<IDataLinqUser>();
        _httpContextMock = new Mock<HttpContext>();
        _razorMock.Setup(r => r.RawString(It.IsAny<string>())).Returns((string input) => new RawContentTestable(input));
        _currentDatalinqService = currentDatalinqService;

        _helper = new DataLinqHelper(_httpContextMock.Object, _currentDatalinqService, _razorMock.Object, _uiMock.Object);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var methods = typeof(DataLinqHelper)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<ExcludeFromSnippetsAttribute>() == null)
            .Where(m => m.Name != nameof(DataLinqHelper.GetCypherPath)) // exclude the method
            .ToArray();


        var methodInfos = new List<MethodInfoObject>();

        var instance = _helper;

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
            var methodDescription = GetDescriptionFromXML(typeof(DataLinqHelper), "en", method, skipper);

            var methodInfo = new MethodInfoObject
            {
                Name = method.Name,
                Parameters = method.GetParameters().Select(p => new ParameterInfo
                {
                    Name = p.Name,
                    Type = TypeToString(p.ParameterType),
                    Description = GetParameterDescriptionFromXML(typeof(DataLinqHelper), p, "en", method, skipper),
                    HasDefaultValue = p.HasDefaultValue,
                    DefaultValue = p.HasDefaultValue ? p.DefaultValue : null,
                    IsOptional = p.IsOptional
                }).ToList(),
                Description = methodDescription,
            };

            try
            {
                var sampleOutput = GetSampleOutput(method, instance);
                methodInfo.SampleOutput = sampleOutput?.ToString() ?? "null";

                methodInfo.FunctionCall = GenerateFunctionCallString(method);
            }
            catch (Exception ex)
            {
                methodInfo.SampleOutput = $"Error calling method: {ex.Message}";
            }

            if (methodInfos.Any(m => m.Name == methodInfo.Name))
            {
                methodInfo.Name = methodInfo.Name + "(Advanced)";
                methodInfos.Add(methodInfo);
            }
            else
                methodInfos.Add(methodInfo);
        }

        var json = JsonConvert.SerializeObject(methodInfos, Formatting.Indented);

        File.WriteAllText("datalinq_helpers_for_copilot.json", json);

        return Task.CompletedTask;
    }


    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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

    private static string GetParameterDescriptionFromXML(Type type, System.Reflection.ParameterInfo parameter, string languageCode, MethodInfo methodInfo, int skipper)
    {
        var xmlFilePath = $"{Path.ChangeExtension(type.Assembly.Location, ".XML")}";
        if (!File.Exists(xmlFilePath))
        {
            return "";
        }

        var xdoc = XDocument.Load(xmlFilePath);

        // Build the member name for the method (same logic as before)
        var memberName = $"M:{methodInfo.DeclaringType.FullName}.{methodInfo.Name}";

        // Find the correct member node, using skipper for overloads
        var member = xdoc
            .Descendants("member")
            .Where(m => m.Attribute("name")?.Value.StartsWith(memberName) == true)
            .Skip(skipper).FirstOrDefault();

        if (member == null)
            return "";

        // Find the <param> node for the parameter
        var paramElement = member.Elements("param")
            .FirstOrDefault(e => e.Attribute("name")?.Value == parameter.Name);

        // If not found, return empty
        if (paramElement == null)
            return "";

        // If your ExtractLanguage extension method is like before, use it to get the description for the specified language
        return paramElement.Value.ExtractLanguage(languageCode);
    }

    private string GenerateFunctionCallString(System.Reflection.MethodInfo method)
    {
        var parameters = method.GetParameters();
        var paramStrings = new List<string>();

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            object sampleValue;

            if (param.HasDefaultValue)
            {
                sampleValue = param.DefaultValue;
            }
            else
            {
                sampleValue = GetSampleValueForType(param.ParameterType, param.Name);
            }

            string valueString = FormatValueForCallString(sampleValue, param.ParameterType, param.Name);
            paramStrings.Add($"{param.Name}: {valueString}");
        }

        return $"@DLH.{method.Name}({string.Join(", ", paramStrings)})";
    }

    private string FormatValueForCallString(object value, Type parameterType, string parameterName = "")
    {
        if (value == null)
            return "null";

        if (parameterName?.ToLower() == "record")
            return "Model.records[0]";

        if (parameterName?.ToLower() == "records")
            return "Model.records";

        return parameterType.Name switch
        {
            "String" => $"\"{value}\"",
            "Boolean" => value.ToString().ToLower(),
            "DateTime" => $"new DateTime({((DateTime)value).Year}, {((DateTime)value).Month}, {((DateTime)value).Day})",
            "Int32" or "Int64" => value.ToString(),
            _ when parameterType.IsArray => "new[] { ... }",
            _ when value is IDictionary => "new Dictionary<string, object> { ... }",
            _ when value is System.Dynamic.ExpandoObject => "new { ... }",
            _ => value.ToString()
        };
    }

    private object GetSampleOutput(System.Reflection.MethodInfo method, object instance)
    {
        var parameters = method.GetParameters();
        var parameterValues = new object[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];

            if (param.HasDefaultValue)
            {
                parameterValues[i] = param.DefaultValue;
            }
            else
            {
                parameterValues[i] = GetSampleValueForType(param.ParameterType, param.Name);
            }
        }

        return method.Invoke(instance, parameterValues);
    }

    private object GetSampleValueForType(Type type, string parameterName)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            type = Nullable.GetUnderlyingType(type);
        }

        return type.Name switch
        {
            "String" => GetSampleStringValue(parameterName),
            "String[]" => GetSampleStringArray(parameterName),
            "Int32" => 1,
            "Boolean" => true,
            "Dictionary`2" when type.GetGenericArguments()[0] == typeof(string) => GetSampleDictionary(parameterName),
            "IEnumerable`1" when type.GetGenericArguments()[0] == typeof(IDictionary<string, object>) => GetSampleRecords(),
            "IDictionary`2[]" when type.GetElementType() == typeof(IDictionary<string, object>) => GetSampleRecords().ToArray(), // Add this line
            "IEnumerable`1" when type.GetGenericArguments()[0] == typeof(string) => GetSampleStringEnumerable(parameterName),
            "Object" => GetSampleObject(parameterName),
            _ => type.IsValueType ? Activator.CreateInstance(type) : null
        };
    }

    private string GetSampleStringValue(string parameterName)
    {
        return parameterName?.ToLower() switch
        {
            "id" => "datalinq-guide@select-all-users-views@index",
            "label" => "Sample Label",
            "field" => "LastName",
            "jsCallbackFuncName" => "jsCallBackFunctionName",
            "filter" => "LastName = Mustermann",
            "orderby" => "-LastName",
            "jsvariablename" => "jsExportVariable",
            "categoryfield" => "IsActive",
            "valuefield" => "IsActive",
            "datetimefield" => "birthday",
            _ => "sample-value"
        };
    }

    private string[] GetSampleStringArray(string parameterName)
    {
        return parameterName?.ToLower() switch
        {
            "columns" => new[] { "firstname", "lastname", "email" },
            _ => new[] { "firstname", "lastname", "email" }
        };
    }

    private Dictionary<string, object> GetSampleDictionary(string parameterName)
    {
        return parameterName?.ToLower() switch
        {
            "orderfields" => new Dictionary<string, object>()
        {
            { "FirstName", new { displayname = "First Name" } },
            { "LastName", new { displayname = "Surname" } },
            { "Email", new { displayname = "E-Mail" } }
        },
            "filterparameters" => new Dictionary<string, object>()
        {
            { "FirstName", new { displayname = "First Name", source = "datalinq-guide@select-all-firstnames", multiple = "multiple", valueField = "FirstName", nameField = "FirstName", prependEmpty = "true" } },
            { "LastName", new { displayname = "Surname", source = "datalinq-guide@select-surnames-where-firstname?FirstName=[FirstName]", valueField = "LastName", nameField = "LastName", prependEmpty = "true" } },
            { "IsActive", new { displayname = "Is User Active", dataType = DataType.Checkbox, hidden = "true" } }
        },
            _ => new Dictionary<string, object> { { "key1", "value1" } }
        };
    }

    private IEnumerable<IDictionary<string, object>> GetSampleRecords()
    {
        return new List<Dictionary<string, object>>
    {
        new Dictionary<string, object>
        {
            { "FirstName", "John" },
            { "LastName", "Doe" },
            { "Email", "john.doe@example.com" },
            { "birthday", new DateTime(1985, 3, 15) },
            { "IsActive", 1 }
        },
        new Dictionary<string, object>
        {
            { "FirstName", "Jane" },
            { "LastName", "Smith" },
            { "Email", "jane.smith@example.com" },
            { "birthday", new DateTime(1990, 7, 22) },
            { "IsActive", 1 }
        },
        new Dictionary<string, object>
        {
            { "FirstName", "Michael" },
            { "LastName", "Johnson" },
            { "Email", "michael.johnson@example.com" },
            { "birthday", new DateTime(1982, 11, 8) },
            { "IsActive", 0 }
        },
        new Dictionary<string, object>
        {
            { "FirstName", "Sarah" },
            { "LastName", "Williams" },
            { "Email", "sarah.williams@example.com" },
            { "birthday", new DateTime(1988, 5, 30) },
            { "IsActive", 1 }
        },
        new Dictionary<string, object>
        {
            { "FirstName", "David" },
            { "lastname", "Brown" },
            { "Email", "david.brown@example.com" },
            { "birthday", new DateTime(1993, 12, 3) },
            { "IsActive", 0 }
        }
    };
    }

    private IEnumerable<string> GetSampleStringEnumerable(string parameterName)
    {
        return parameterName?.ToLower() switch
        {
            "columns" => new[] { "firstname", "lastname", "email" },
            _ => new[] { "firstname", "lastname", "email" }
        };
    }

    private object GetSampleObject(string parameterName)
    {
        dynamic expando = new ExpandoObject();
        expando.FirstName = "Max";
        expando.LastName = "Mustermann";
        expando.Email = "max@mustermann.at";
        expando.birthday = new DateTime(1985, 3, 15);
        expando.IsActive = 0;
        return expando;
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

    public class MethodInfoObject
    {
        public string Name { get; set; }
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();
        public string SampleOutput { get; set; }
        public string FunctionCall { get; set; }
        public string Description { get; set; }
    }

    public class ParameterInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public bool HasDefaultValue { get; set; }
        public object DefaultValue { get; set; }
        public bool IsOptional { get; set; }
    }

    public class RawContentTestable
    {
        public RawContentTestable(object value)
        {
            Value = value;
        }

        public object Value { get; }

        public override string ToString() => Value?.ToString() ?? "";

        public override bool Equals(object? obj)
        {
            return obj is RawContentTestable other &&
                   other.ToString() == this.ToString();
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
