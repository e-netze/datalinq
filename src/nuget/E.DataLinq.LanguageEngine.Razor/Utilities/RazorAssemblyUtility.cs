using E.DataLinq.LanguageEngine.Razor.Abstractions;
using E.DataLinq.LanguageEngine.Razor.Compilation;

namespace E.DataLinq.LanguageEngine.Razor.Utilities;
public class RazorAssemblyUtility
{
    public static async Task<IRazorAssembly<T>> LoadFromFileAsync<T>(string fileName)
        where T : IRazorEngineTemplate
    {

        await using (FileStream fileStream = new FileStream(
                         path: fileName,
                         mode: FileMode.Open,
                         access: FileAccess.Read,
                         share: FileShare.None,
                         bufferSize: 4096,
                         useAsync: true))
        {
            return await LoadFromStreamAsync<T>(fileStream);
        }
    }

    public static async Task<IRazorAssembly<T>> LoadFromStreamAsync<T>(Stream stream)
        where T : IRazorEngineTemplate
    {
        return new RazorAssembly<T>(await RazorAssemblyMeta.Read(stream));
    }

    public static AssemlyLoadContextStragegy AssemlyLoadContextStragegy = AssemlyLoadContextStragegy.Collectible;
}
