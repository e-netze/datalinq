using E.DataLinq.Core.Models;

namespace E.DataLinq.Core.Services.Abstraction;

public interface IDataLinqEnvironmentService
{
    string GetConnectionString(DataLinqEndPoint endPoint);
}
