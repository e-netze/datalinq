using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using System.Reflection;

namespace E.DataLinq.LanguageEngine.Razor.Abstractions;

public interface IRazorEngineCompilationOptionsBuilder
{
    internal DataLinqRazorEngineCompilationOptions Options { get; set; }

    void AddAssemblyReferenceByName(string assemblyName);

    void AddAssemblyReference(Assembly assembly);

    void AddAssemblyReference(Type type);

    void AddMetadataReference(MetadataReference reference);

    void AddUsing(string namespaceName);

    void Inherits(Type type);

    void IncludeDebuggingInfo();

    void ConfigureRazorEngineProject(Action<RazorProjectEngineBuilder> configure);
}
