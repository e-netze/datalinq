using E.DataLinq.Core.Services.Abstraction;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Services.Abstraction;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services.Worker;

public class DeleteDataLinqRazorEngineTempFilesWorkerService : IWorkerService
{
    private readonly ILogger<DeleteDataLinqRazorEngineTempFilesWorkerService> _logger;
    private readonly IBinaryCache _binaryCache;

    public DeleteDataLinqRazorEngineTempFilesWorkerService(
            ILogger<DeleteDataLinqRazorEngineTempFilesWorkerService> logger,
            IBinaryCache binaryCache
        )
    {
        _logger = logger;
        _binaryCache = binaryCache;
    }

    public int DurationSeconds => 3600; // 1h;
    public void DoWork()
    {
        try
        {
            _logger.LogInformation("Deleting DataLinq RazorEngine Temp Files");

            _binaryCache.Cleanup("*".ToRazorAssemblyFilename());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while deleting DataLinq RazorEngine Temp Files");
        }
    }
    public Task<bool> Init()
    {
        return Task.FromResult(true);
    }
}
