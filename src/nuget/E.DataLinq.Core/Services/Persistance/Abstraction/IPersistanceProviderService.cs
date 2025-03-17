using E.DataLinq.Core.Models;
using E.DataLinq.Core.Models.Authentication;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace E.DataLinq.Core.Services.Persistance.Abstraction;

public interface IPersistanceProviderService
{
    Task<DataLinqEndPoint> GetEndPoint(string endPointId);
    Task<DataLinqEndPointQuery> GetEndPointQuery(string endPointId, string endPointQueryId);
    Task<DataLinqEndPointQueryView> GetEndPointQueryView(string endPointId, string endPointQueryId, string endPointQueryViewId);
    Task<string> GetEndPointCss(string endPointId);
    Task<string> GetEndPointJavascript(string endPointId);

    Task<bool> DeleteEndPoint(string endPointId);
    Task<bool> DeleteEndPointQuery(string endPointId, string endPointQueryId);
    Task<bool> DeleteEndPointQueryView(string endPointId, string endPointQueryId, string endPointQueryViewId);

    Task<IDictionary<string, IEnumerable<string>>> GetEndPointPrefixes();
    Task<IEnumerable<string>> GetEndPointIds(IEnumerable<string> filters);
    Task<IEnumerable<string>> GetQueryIds(string endPointId);
    Task<IEnumerable<string>> GetViewIds(string endPointId, string queryId);

    Task<bool> StoreEndPoint(DataLinqEndPoint endPoint);
    Task<bool> StoreEndPointQuery(DataLinqEndPointQuery endPointQuery);
    Task<bool> StoreEndPointQueryView(DataLinqEndPointQueryView endPointQueryView);
    Task<bool> StoreEndPointCss(string endPointId, string css);
    Task<bool> StoreEndPointJavascript(string endPointId, string css);

    Task<string> EndPointCreator(string endPointId);

    DataLinqCodeIdentity CurrentCodeIdentity { get; }
}
