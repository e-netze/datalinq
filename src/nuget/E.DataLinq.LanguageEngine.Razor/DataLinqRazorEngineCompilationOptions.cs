using E.DataLinq.LanguageEngine.Razor.Templates;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;

namespace E.DataLinq.LanguageEngine.Razor;

class DataLinqRazorEngineCompilationOptions
{
    public HashSet<Assembly>? ReferencedAssemblies { get; set; }

    public HashSet<MetadataReference> MetadataReferences { get; set; } = new HashSet<MetadataReference>();
    public string TemplateNamespace { get; set; } = "TemplateNamespace";
    public string TemplateFilename { get; set; } = "";
    public string Inherits { get; set; } = "RazorEngineCore.RazorEngineTemplateBase";
    ///Set to true to generate PDB symbols information along with the assembly for debugging support
    public bool IncludeDebuggingInfo { get; set; } = false;
    public HashSet<string> DefaultUsings { get; set; } = new HashSet<string>()
        {
            "System.Linq",
            "System.Collections",
            "System.Collections.Generic"
        };
    public Action<RazorProjectEngineBuilder>? ProjectEngineBuilder { get; set; }

    public DataLinqRazorEngineCompilationOptions()
    {
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        bool isFullFramework = RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase);

        if (isWindows && isFullFramework)
        {
            ReferencedAssemblies = new HashSet<Assembly>()
                {
                    typeof(object).Assembly,
                    Assembly.Load(new AssemblyName("Microsoft.CSharp, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")),
                    typeof(RazorEngineTemplate).Assembly,
                    typeof(System.Runtime.GCSettings).Assembly,
                    typeof(IList).Assembly,
                    typeof(IEnumerable<>).Assembly,
                    typeof(Enumerable).Assembly,
                    typeof(System.Linq.Expressions.Expression).Assembly,
                    Assembly.Load(new AssemblyName("netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51"))
                };
        }

        if (isWindows && !isFullFramework) // i.e. NETCore
        {
            ReferencedAssemblies = new HashSet<Assembly>()
                {
                    typeof(object).Assembly,
                    Assembly.Load(new AssemblyName("Microsoft.CSharp")),
                    typeof(RazorEngineTemplate).Assembly,
                    Assembly.Load(new AssemblyName("System.Runtime")),
                    typeof(IList).Assembly,
                    typeof(IEnumerable<>).Assembly,
                    Assembly.Load(new AssemblyName("System.Linq")),
                    Assembly.Load(new AssemblyName("System.Linq.Expressions")),
                    Assembly.Load(new AssemblyName("netstandard"))
                };
        }

        if (!isWindows)
        {
            ReferencedAssemblies = new HashSet<Assembly>()
                {
                    typeof(object).Assembly,
                    Assembly.Load(new AssemblyName("Microsoft.CSharp")),
                    typeof(RazorEngineTemplate).Assembly,
                    Assembly.Load(new AssemblyName("System.Runtime")),
                    typeof(IList).Assembly,
                    typeof(IEnumerable<>).Assembly,
                    Assembly.Load(new AssemblyName("System.Linq")),
                    Assembly.Load(new AssemblyName("System.Linq.Expressions")),
                    Assembly.Load(new AssemblyName("netstandard"))
                };
        }
    }
}
