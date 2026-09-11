using E.DataLinq.Core.Models;

namespace E.DataLinq.Core.Services.Abstraction;

public interface IDataLinqEnvironmentService
{
    DataLinqEnvironmentType CurrentEnvironment { get; }

    string GetConnectionString(DataLinqEndPoint endPoint);
}
