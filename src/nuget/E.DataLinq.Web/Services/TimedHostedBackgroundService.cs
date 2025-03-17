using E.DataLinq.Web.Services.Abstraction;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services;

class TimedHostedBackgroundService : IHostedService, IDisposable
{
    private Timer _timer;
    private int counter = 0;
    private bool _working = false;

    private readonly IEnumerable<IWorkerService> _workers;

    public TimedHostedBackgroundService(IEnumerable<IWorkerService> workers = null)
    {
        _workers = workers;
    }

    #region IDisposable

    public void Dispose()
    {
        _timer?.Dispose();
    }

    #endregion

    #region IHostedService

    async public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_workers != null && _workers.Count() > 0)
        {
            foreach (var worker in _workers)
            {
                await worker.Init();
            }

            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    #endregion

    private void DoWork(object state)
    {
        if (_working)
        {
            return;
        }

        try
        {
            _working = true;

            if (_workers != null)
            {
                foreach (var worker in _workers)
                {
                    if (counter % worker.DurationSeconds == 0)
                    {
                        try
                        {
                            worker.DoWork();
                        }
                        catch { }
                    }
                }
            }

            counter++;
            if (counter >= 86400)
            {
                counter = 0;
            }
        }
        finally
        {
            _working = false;
        }
    }
}
