namespace E.DataLinq.LanguageEngine.Razor.Abstractions;

public interface IRazorAssembly<out T> : IDisposable
    where T : IRazorEngineTemplate
{
    void SaveToStream(Stream stream);
    Task SaveToStreamAsync(Stream stream);
    void SaveToFile(string fileName);
    Task SaveToFileAsync(string fileName);
    void EnableDebugging(string? debuggingOutputDirectory = null);
    string Run(Action<T> initializer);
    Task<string> RunAsync(Action<T> initializer);
}
