using E.DataLinq.Core.Services.Abstraction;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace E.DataLinq.Core.Services.Cache;
public class BinaryFileCache : IBinaryCache
{
    private readonly string _rootFolder;
    private readonly string _sessionFolder;
    private readonly ILogger<BinaryFileCache> _logger;

    public BinaryFileCache(
            ILogger<BinaryFileCache> logger,
            string rootFolder,
            bool randomSub = false
        )
    {
        _logger = logger;

        var randomSubName = randomSub
                ? Guid.NewGuid().ToString("N")
                : "";

        _rootFolder = rootFolder;
        _sessionFolder = Path.Combine(rootFolder, randomSubName);

        if (!Directory.Exists(_sessionFolder))
        {
            Directory.CreateDirectory(_sessionFolder);
        }
    }

    public bool HasData(string key, string @namespace = "")
    {
        var path = Path.Combine(_sessionFolder, @namespace, key);
        return File.Exists(path);
    }

    public Task<byte[]> GetBytes(string key, string @namespace = "")
    {
        var path = Path.Combine(_sessionFolder, @namespace, key);
        if (!File.Exists(path))
        {
            return Task.FromResult<byte[]>(null);
        }

        return File.ReadAllBytesAsync(path);
    }

    async public Task SetBytes(string key, byte[] bytes, string @namespace = "")
    {
        var folder = Path.Combine(_sessionFolder, @namespace);
        var path = Path.Combine(_sessionFolder, @namespace, key);

        using (var mutex = await FuzzyMutexAsync.LockAsync(path))
        { 
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            await File.WriteAllBytesAsync(path, bytes);
        }
    }

    public void Remove(string key, string @namespace = "")
    {
        var path = Path.Combine(_sessionFolder, @namespace, key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void Cleanup(string filter)
    {
        if (!Directory.Exists(_rootFolder))
        {
            return;
        }

        #region Remove Files older than 1 day

        var fileNames = Directory.GetFiles(_rootFolder, filter, SearchOption.AllDirectories);

        foreach (var fileName in fileNames)
        {
            var fileInfo = new FileInfo(fileName);

            if (fileInfo.Exists && fileInfo.CreationTimeUtc < DateTime.UtcNow.AddDays(-1))
            {
                _logger.LogInformation("Deleting file: {fileName}", fileInfo.FullName);

                fileInfo.Delete();
            }
        }

        #endregion

        #region Remove empty directories

        var directories = Directory.GetDirectories(_rootFolder, "*", SearchOption.AllDirectories)
                                   .Where(d => d != _sessionFolder)
                                   .OrderByDescending(d => d.Length)
                                   .ToArray();

        foreach (var directory in directories)
        {
            if (Directory.GetFiles(directory).Length == 0 &&
                Directory.GetDirectories(directory).Length == 0)
            {
                _logger.LogInformation("Deleting empty directory: {directory}", directory);

                Directory.Delete(directory, false);
            }
        }

        #endregion
    }
}
