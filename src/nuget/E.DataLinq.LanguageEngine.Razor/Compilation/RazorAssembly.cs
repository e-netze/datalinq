using E.DataLinq.LanguageEngine.Razor.Abstractions;
using E.DataLinq.LanguageEngine.Razor.Exceptions;
using E.DataLinq.LanguageEngine.Razor.Templates;
using E.DataLinq.LanguageEngine.Razor.Utilities;
using System.Reflection;
using System.Runtime.Loader;

namespace E.DataLinq.LanguageEngine.Razor.Compilation;

class RazorAssembly<T> :
        IDisposable,
        IRazorAssembly<T> where T : IRazorEngineTemplate
{
    private readonly RazorAssemblyMeta _meta;
    private AssemblyLoadContext? _context;
    private T? _template;

    private bool _disposed = false;

    internal RazorAssembly(RazorAssemblyMeta meta)
    {
        _meta = meta;

        var assembly = RazorAssemblyUtility.AssemlyLoadContextStragegy == AssemlyLoadContextStragegy.Collectible
            ? CreateAssemblyInContext()
            : CreateAssembly();

        var type =
            assembly
                .GetTypes()
                .Where(x => x.Namespace == _meta.TemplateNamespace)
                .FirstOrDefault(x => typeof(T).IsAssignableFrom(x) && !x.IsAbstract)

            ?? throw new InvalidOperationException(
                $"Type {typeof(T).FullName} not found in assembly"
            );

        _template =
            (T)Activator.CreateInstance(type)!
            ?? throw new InvalidOperationException($"Failed to create instance of {type.FullName}");
    }

    ~RazorAssembly()
    {
        Dispose(false);
    }

    private Assembly CreateAssembly() => Assembly.Load(_meta.AssemblyByteCode!, _meta.PdbByteCode);

    private Assembly CreateAssemblyInContext()
    {
        _context = new AssemblyLoadContext(null, isCollectible: true);

        using var ms = new MemoryStream(_meta.AssemblyByteCode!);
        var assembly = _context.LoadFromStream(ms);

        return assembly;
    }

    #region Members

    protected bool IsDebuggerEnabled { get; set; }

    public void SaveToFile(string fileName)
    {
        SaveToFileAsync(fileName).GetAwaiter().GetResult();
    }

    public async Task SaveToFileAsync(string fileName)
    {

        await using (FileStream fileStream = new FileStream(
                   path: fileName,
                   mode: FileMode.OpenOrCreate,
                   access: FileAccess.Write,
                   share: FileShare.None,
                   bufferSize: 4096,
                   useAsync: true))
        {
            await SaveToStreamAsync(fileStream);
        }
    }

    public void SaveToStream(Stream stream)
    {
        SaveToStreamAsync(stream).GetAwaiter().GetResult();
    }

    public Task SaveToStreamAsync(Stream stream)
    {
        return _meta.Write(stream) ?? Task.CompletedTask;
    }

    public void EnableDebugging(string? debuggingOutputDirectory = null)
    {
        if (_meta.PdbByteCode == null || _meta.PdbByteCode.Length == 0 || string.IsNullOrWhiteSpace(_meta.TemplateSource))
        {
            throw new RazorEngineException("No debugging info available, compile template with builder.IncludeDebuggingInfo(); option");
        }

        File.WriteAllText(Path.Combine(debuggingOutputDirectory ?? ".", _meta.TemplateFileName!), _meta.TemplateSource);

        IsDebuggerEnabled = true;
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _context?.Unload();

            _context = null;
            _template = default;
        }

        _disposed = true;
    }

    #endregion

    #region Static Members

    public static RazorAssembly<T> LoadFromFile(string fileName)
    {
        return LoadFromFileAsync(fileName).GetAwaiter().GetResult();
    }

    public static async Task<RazorAssembly<T>> LoadFromFileAsync(string fileName)
    {

        await using (FileStream fileStream = new FileStream(
                         path: fileName,
                         mode: FileMode.Open,
                         access: FileAccess.Read,
                         share: FileShare.None,
                         bufferSize: 4096,
                         useAsync: true))
        {
            return await LoadFromStreamAsync(fileStream);
        }
    }

    public static IRazorAssembly<T> LoadFromStream(Stream stream)
    {
        return LoadFromStreamAsync(stream).GetAwaiter().GetResult();
    }

    public static async Task<RazorAssembly<T>> LoadFromStreamAsync(Stream stream)
    {
        return new RazorAssembly<T>(await RazorAssemblyMeta.Read(stream));
    }

    public string Run(Action<T> initializer)
    {
        return RunAsync(initializer).GetAwaiter().GetResult();
    }

    public async Task<string> RunAsync(Action<T> initializer)
    {
        ArgumentNullException.ThrowIfNull(_template);

        initializer(_template);

        if (IsDebuggerEnabled && _template is RazorEngineTemplate instance2)
        {
            instance2.Breakpoint = System.Diagnostics.Debugger.Break;
        }

        await _template.ExecuteAsync();

        return await _template.ResultAsync();
    }

    #endregion 
}
