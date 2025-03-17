using E.DataLinq.LanguageEngine.Razor.Abstractions;
using E.DataLinq.Web.Models;
using E.DataLinq.Web.Services;

namespace E.DataLinq.Web.Extensions;
static internal class RazorEngineCompilationOptionsBuilderExtensions
{
    static public void AddDefaults(this IRazorEngineCompilationOptionsBuilder builder, DataLinqOptions options)
    {
        builder.AddAssemblyReference(typeof(SelectResult).Assembly);
        builder.AddAssemblyReferenceByName("System.Collections");
        builder.AddAssemblyReferenceByName("System.Collections.Specialized");
        //builder.AddAssemblyReferenceByName("System.Collections, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
        //builder.AddAssemblyReferenceByName("System.Collections.Specialized, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

        foreach (var assembly in options.AssemblyReferences)
        {
            builder.AddAssemblyReference(assembly);
        }

        builder.AddUsing("System");
        builder.AddUsing("System");
        builder.AddUsing("System.Collections");
        builder.AddUsing("System.Collections.Generic");
    }
}
