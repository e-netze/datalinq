using E.DataLinq.Web.Services.Abstraction;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services.Worker;

public class DeleteRazorEngineTempFilesWorkerService : IWorkerService
{
    public int DurationSeconds => 86400; // 24 * 60 * 60 => 24h;

    public void DoWork()
    {
        #region Delete RazorEngine Temp Files

        try
        {
            var tempDir = new DirectoryInfo(Path.GetTempPath());
            foreach (var razorEngineTempDir in tempDir.GetDirectories("RazorEngine_*")
                                                     .Where(d => (DateTime.UtcNow - d.LastWriteTimeUtc).TotalDays > 1)
                                                     .Take(100)
                                                     .ToArray())
            {
                try
                {
                    razorEngineTempDir.Delete(true);
                }
                catch { }
            }
        }
        catch
        {

        }

        #endregion
    }

    public Task<bool> Init()
    {
        return Task.FromResult(true);
    }
}
