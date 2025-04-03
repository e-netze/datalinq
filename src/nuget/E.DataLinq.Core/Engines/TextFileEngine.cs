#nullable enable

using E.DataLinq.Core.Engines.Abstraction;
using E.DataLinq.Core.Engines.Models;
using E.DataLinq.Core.Extensions;
using E.DataLinq.Core.IO;
using E.DataLinq.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace E.DataLinq.Core.Engines;

public class TextFileEngine : IDataLinqSelectEngine
{
    private readonly ILogger<TextFileEngine> _logger;
    private readonly TextFileEngineOptions _options;

    public TextFileEngine(
            ILogger<TextFileEngine> logger,
            IOptions<TextFileEngineOptions> options
        )
    {
        _logger = logger;
        _options = options.Value;
    }

    public int EndpointType => (int)DefaultEndPointTypes.TextFile;

    async public Task<(object[] records, bool isOrdered)> SelectAsync(
                DataLinqEndPoint endPoint,
                DataLinqEndPointQuery query,
                NameValueCollection arguments
        )
    {
        var connection = TextReaderConnection.FromQueryStatement(
            query.Statement
                .ParseStatement(arguments)
                .ReplacePlaceholders(arguments));
        var fileInfo = new FileInfo(Path.Combine(endPoint.ConnectionString, connection.File));

        string fileDirectory = fileInfo.Directory!.FullName.AddPathSeparator();

        if (_options.AllowedPaths.Any() &&
            !_options.AllowedPaths.Any(p => fileDirectory.IsInPath(p)))
        {
            throw new IOException($"Path '{fileDirectory}' is not allowed");
        }

        if (_options.AllowedExtensions.Any() &&
            !_options.AllowedExtensions.Any(e => fileInfo.FullName.HasFileExtension(e)))
        {
            throw new IOException($"Extension '{fileInfo.Extension}' is not allowed");
        }

        if (!fileInfo.Exists)
        {
            throw new IOException($"File {fileInfo.FullName} not exists");
        }

        var lines = connection.From switch
        {
            ReadFrom.Bottom => await FileEx.ReadBottomLinesAsync(fileInfo.FullName, connection.MaxLines, connection.Filter),
            _ => await FileEx.ReadTopLinesAsync(fileInfo.FullName, connection.MaxLines, connection.Filter)
        };

        var records = lines
            .Select(l => l.ToRecord(fileInfo.Extension))
            .ToArray();

        return (records, false);
    }

    public Task<bool> TestConnection(DataLinqEndPoint endPoint)
    {
        return Task.FromResult(Directory.Exists(endPoint.ConnectionString));
    }
}

public class TextFileEngineOptions
{
    public string[] AllowedPaths { get; set; } = [];
    public string[] AllowedExtensions { get; set; } = [];

    static public TextFileEngineOptions Default => new TextFileEngineOptions()
    {
        AllowedPaths = Platform.IsWindows
            ? ["C:\\DataLinq\\Data\\"]
            : ["/etc/datalinq/data/"],

        AllowedExtensions = [".txt", ".csv"]
    };
}
