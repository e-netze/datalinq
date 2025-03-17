using E.DataLinq.Core.Models;
using System.Collections.Specialized;
using System.Threading.Tasks;

namespace E.DataLinq.Core.Engines.Abstraction;

public interface IDataLinqExecuteNonQueryEngine
{
    Task<bool> ExecuteNonQueryAsync(DataLinqEndPoint endPoint, DataLinqEndPointQuery query, NameValueCollection form);
}
