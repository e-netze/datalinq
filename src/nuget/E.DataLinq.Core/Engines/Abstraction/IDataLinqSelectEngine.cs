using E.DataLinq.Core.Models;
using System.Collections.Specialized;
using System.Threading.Tasks;

namespace E.DataLinq.Core.Engines.Abstraction;

public interface IDataLinqSelectEngine
{
    //EndPointType EndpointType { get; }
    int EndpointType { get; }

    Task<bool> TestConnection(DataLinqEndPoint endPoint);

    Task<(object[] records, bool isOrdered)> SelectAsync(DataLinqEndPoint endPoint, DataLinqEndPointQuery query, NameValueCollection arguments);
}
