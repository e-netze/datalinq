using E.DataLinq.LanguageEngine.Razor.Abstractions;
using E.DataLinq.LanguageEngine.Razor.Compilation;
using E.DataLinq.LanguageEngine.Razor.Exceptions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System.Text;

namespace E.DataLinq.LanguageEngine.Razor;

public class DataLinqRazorEngine
{
    public IRazorAssembly<T> Compile<T>(string content, Action<IRazorEngineCompilationOptionsBuilder>? builderAction = null, CancellationToken cancellationToken = default) where T : IRazorEngineTemplate
    {
        IRazorEngineCompilationOptionsBuilder compilationOptionsBuilder = new DataLinqRazorEngineCompilationOptionsBuilder();
        compilationOptionsBuilder.AddAssemblyReference(typeof(T).Assembly);
        compilationOptionsBuilder.Inherits(typeof(T));

        builderAction?.Invoke(compilationOptionsBuilder);

        RazorAssemblyMeta meta = CreateAndCompileToStream(content, compilationOptionsBuilder.Options, cancellationToken);

        return new RazorAssembly<T>(meta);
    }

    public Task<IRazorAssembly<T>> CompileAsync<T>(string content, Action<IRazorEngineCompilationOptionsBuilder>? builderAction = null, CancellationToken cancellationToken = default) where T : IRazorEngineTemplate
    {
        return Task.Run(() => Compile<T>(content: content, builderAction: builderAction, cancellationToken: cancellationToken));
    }

    private RazorAssemblyMeta CreateAndCompileToStream(string templateSource, DataLinqRazorEngineCompilationOptions options, CancellationToken cancellationToken)
    {
        templateSource = WriteDirectives(templateSource, options);

        //string projectPath = @".";
        string projectPath = Path.Combine(Path.GetTempPath(), "datalinq_razorengine");

        string fileName = string.IsNullOrWhiteSpace(options.TemplateFilename)
            ? Path.GetRandomFileName() + ".cshtml"
            : options.TemplateFilename;

        RazorProjectEngine engine = RazorProjectEngine.Create(
            RazorConfiguration.Default,
            RazorProjectFileSystem.Create(projectPath),
            (builder) =>
            {
                builder.SetNamespace(options.TemplateNamespace);
                options.ProjectEngineBuilder?.Invoke(builder);
            });

        RazorSourceDocument document = RazorSourceDocument.Create(templateSource, fileName);

        RazorCodeDocument codeDocument = engine.Process(
            document,
            null,
            new List<RazorSourceDocument>(),
            new List<TagHelperDescriptor>());

        RazorCSharpDocument razorCSharpDocument = codeDocument.GetCSharpDocument();
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(razorCSharpDocument.GeneratedCode, cancellationToken: cancellationToken);

        CSharpCompilation compilation = CSharpCompilation.Create(
            fileName,
            new[]
            {
                    syntaxTree
            },
            options.ReferencedAssemblies?
               .Select(ass =>
               {
                   // save variant... not testet
                   return MetadataReference.CreateFromFile(ass.Location);

                   // unsafe (original) variant
                   //unsafe
                   //{
                   //    ass.TryGetRawMetadata(out byte* blob, out int length);
                   //    ModuleMetadata moduleMetadata = ModuleMetadata.CreateFromMetadata((nint)blob, length);
                   //    AssemblyMetadata assemblyMetadata = AssemblyMetadata.Create(moduleMetadata);
                   //    PortableExecutableReference metadataReference = assemblyMetadata.GetReference();

                   //    return metadataReference;
                   //}
               })
                .Concat(options.MetadataReferences)
                .ToList(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithOverflowChecks(true));


        MemoryStream assemblyStream = new MemoryStream();
        MemoryStream? pdbStream = options.IncludeDebuggingInfo
            ? new MemoryStream()
            : null;

        EmitResult emitResult = compilation.Emit(assemblyStream, pdbStream, cancellationToken: cancellationToken);

        if (!emitResult.Success)
        {
            RazorEngineCompilationException exception = new RazorEngineCompilationException(
                errors: emitResult.Diagnostics.ToList(),
                generatedCode: razorCSharpDocument.GeneratedCode
            );

            throw exception;
        }

        return new RazorAssemblyMeta()
        {
            AssemblyByteCode = assemblyStream.ToArray(),
            PdbByteCode = pdbStream?.ToArray(),
            GeneratedSourceCode = options.IncludeDebuggingInfo ? razorCSharpDocument.GeneratedCode : null,
            TemplateSource = options.IncludeDebuggingInfo ? templateSource : null,
            TemplateNamespace = options.TemplateNamespace,
            TemplateFileName = fileName
        };
    }

    private string WriteDirectives(string content, DataLinqRazorEngineCompilationOptions options)
    {
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"@inherits {options.Inherits}");

        foreach (string entry in options.DefaultUsings)
        {
            stringBuilder.AppendLine($"@using {entry}");
        }

        stringBuilder.Append(content);

        return stringBuilder.ToString();
    }
}
