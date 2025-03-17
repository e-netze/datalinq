using E.DataLinq.LanguageEngine.Razor.Abstractions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using System.Reflection;

namespace E.DataLinq.LanguageEngine.Razor;

class DataLinqRazorEngineCompilationOptionsBuilder : IRazorEngineCompilationOptionsBuilder
{
    public DataLinqRazorEngineCompilationOptions Options { get; set; }

    public DataLinqRazorEngineCompilationOptionsBuilder(DataLinqRazorEngineCompilationOptions? options = null)
    {
        Options = options ?? new DataLinqRazorEngineCompilationOptions();
    }

    public void AddAssemblyReferenceByName(string assemblyName)
    {
        Assembly assembly = Assembly.Load(new AssemblyName(assemblyName));
        AddAssemblyReference(assembly);
    }

    public void AddAssemblyReference(Assembly assembly)
    {
        Options.ReferencedAssemblies?.Add(assembly);
    }

    public void AddAssemblyReference(Type type)
    {
        AddAssemblyReference(type.Assembly);

        foreach (Type argumentType in type.GenericTypeArguments)
        {
            AddAssemblyReference(argumentType);
        }
    }

    public void AddMetadataReference(MetadataReference reference)
    {
        Options.MetadataReferences.Add(reference);
    }

    public void AddUsing(string namespaceName)
    {
        Options.DefaultUsings.Add(namespaceName);
    }

    public void Inherits(Type type)
    {
        Options.Inherits = RenderTypeName(type);
        AddAssemblyReference(type);
    }

    private string RenderTypeName(Type type)
    {
        IList<string> elements = new List<string>()
            {
                type.Namespace ?? string.Empty,
                RenderDeclaringType(type.DeclaringType) ?? string.Empty,
                type.Name
            };

        string result = string.Join(".", elements.Where(e => !string.IsNullOrWhiteSpace(e)));

        int tildeLocation = result.IndexOf('`');
        if (tildeLocation > -1)
        {
            result = result.Substring(0, tildeLocation);
        }

        if (type.GenericTypeArguments.Length == 0)
        {
            return result;
        }

        return result + "<" + string.Join(",", type.GenericTypeArguments.Select(RenderTypeName)) + ">";
    }

    private string? RenderDeclaringType(Type? type)
    {
        if (type == null)
        {
            return null;
        }

        string? parent = RenderDeclaringType(type.DeclaringType);

        if (string.IsNullOrWhiteSpace(parent))
        {
            return type.Name;
        }

        return parent + "." + type.Name;
    }

    public void IncludeDebuggingInfo()
    {
        Options.IncludeDebuggingInfo = true;
    }

    public void ConfigureRazorEngineProject(Action<RazorProjectEngineBuilder> configure)
    {
        Options.ProjectEngineBuilder = configure;
    }

}
