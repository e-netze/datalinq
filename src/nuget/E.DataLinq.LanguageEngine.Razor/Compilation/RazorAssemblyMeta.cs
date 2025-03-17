using E.DataLinq.LanguageEngine.Razor.Exceptions;
using E.DataLinq.LanguageEngine.Razor.Extensions;
using System.Text;

namespace E.DataLinq.LanguageEngine.Razor.Compilation;

class RazorAssemblyMeta
{
    public byte[]? AssemblyByteCode { get; set; }
    public byte[]? PdbByteCode { get; set; }
    public string? GeneratedSourceCode { get; set; }
    public string? TemplateNamespace { get; set; } = "TemplateNamespace";
    public string? TemplateSource { get; set; }
    public string? TemplateFileName { get; set; }

    public async Task Write(Stream stream)
    {
        await stream.WriteLong(10001);

        await WriteBuffer(stream, AssemblyByteCode ?? Array.Empty<byte>());
        await WriteBuffer(stream, PdbByteCode ?? Array.Empty<byte>());
        await WriteString(stream, GeneratedSourceCode ?? string.Empty);
        await WriteString(stream, TemplateSource ?? string.Empty);
        await WriteString(stream, TemplateNamespace ?? string.Empty);
        await WriteString(stream, TemplateFileName ?? string.Empty);
    }

    public static async Task<RazorAssemblyMeta> Read(Stream stream)
    {
        long version = await stream.ReadLong();

        if (version == 10001)
        {
            return await LoadVersion1(stream);
        }

        throw new RazorEngineException("Unable to load template: wrong version");
    }

    private static async Task<RazorAssemblyMeta> LoadVersion1(Stream stream)
    {
        return new RazorAssemblyMeta()
        {
            AssemblyByteCode = await ReadBuffer(stream),
            PdbByteCode = await ReadBuffer(stream),
            GeneratedSourceCode = await ReadString(stream),
            TemplateSource = await ReadString(stream),
            TemplateNamespace = await ReadString(stream),
            TemplateFileName = await ReadString(stream),
        };
    }

    private Task WriteString(Stream stream, string value)
    {
        byte[]? buffer = value == null
            ? null :
            Encoding.UTF8.GetBytes(value);

        return WriteBuffer(stream, buffer);
    }

    private async Task WriteBuffer(Stream stream, byte[]? buffer)
    {
        if (buffer == null)
        {
            await stream.WriteLong(0);
            return;
        }

        await stream.WriteLong(buffer.Length);
        await stream.WriteAsync(buffer);
    }

    private static async Task<string?> ReadString(Stream stream)
    {
        byte[]? buffer = await ReadBuffer(stream);
        return buffer == null
            ? null
            : Encoding.UTF8.GetString(buffer);
    }

    private static async Task<byte[]?> ReadBuffer(Stream stream)
    {
        long length = await stream.ReadLong();

        if (length == 0)
        {
            return null;
        }

        byte[] buffer = new byte[length];

        _ = await stream.ReadAsync(buffer);

        return buffer;
    }
}
