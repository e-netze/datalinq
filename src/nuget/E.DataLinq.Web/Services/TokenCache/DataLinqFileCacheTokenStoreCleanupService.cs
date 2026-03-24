using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services.TokenCache;

internal class DataLinqFileCacheTokenStoreCleanupService : BackgroundService
{
    private readonly TokenStoreOptions _options;
    private readonly ILogger<DataLinqFileCacheTokenStoreCleanupService> _logger;
    private readonly DataLinqFileCacheTokenStore _fileTokenStore;

    public DataLinqFileCacheTokenStoreCleanupService(
        IOptions<TokenStoreOptions> options,
        ILogger<DataLinqFileCacheTokenStoreCleanupService> logger,
        IDataLinqCacheTokenStore tokenStore
        )
    {
        _options = options.Value;
        _logger = logger;
        _fileTokenStore = tokenStore as DataLinqFileCacheTokenStore;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await DoCleanupAsync(stoppingToken);
            await Task.Delay(_options.CleanupIntervalMinutes, stoppingToken);
        }
    }

    private async Task DoCleanupAsync(CancellationToken ct)
    {
        var folderPath = _options.FilePath;
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        if (!Directory.Exists(folderPath))
            return;

        int deleted = 0, scanned = 0, invalid = 0;

        foreach (var file in Directory.EnumerateFiles(folderPath, "*.token", SearchOption.TopDirectoryOnly))
        {
            scanned++;
            ct.ThrowIfCancellationRequested();

            try
            {
                var tokenGuid = Path.GetFileNameWithoutExtension(file);

                var revokeResult = await _fileTokenStore.RevokeIfExpired(tokenGuid);

                if (revokeResult)
                    deleted++;
                else
                    invalid++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping token file {File} because it could not be read/parsed.", file);
                continue;
            }

        }
        _logger.LogInformation(
            "FileStoreToken cleanup complete. Scanned={Scanned}, Deleted={Deleted}, Invalid={Invalid}, Folder={Folder}",
            scanned, deleted, invalid, folderPath);
    }

}