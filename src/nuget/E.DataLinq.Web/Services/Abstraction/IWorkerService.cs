using System.Threading.Tasks;

namespace E.DataLinq.Web.Services.Abstraction;

public interface IWorkerService
{
    int DurationSeconds { get; }

    Task<bool> Init();

    void DoWork();
}
