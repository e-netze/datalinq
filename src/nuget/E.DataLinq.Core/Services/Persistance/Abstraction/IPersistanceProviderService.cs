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
    Task<string> GetViewCss(string id);
    Task<string> GetEndPointJavascript(string endPointId);
    Task<string> GetViewJs(string id);
    Task<string> GetErrorPage(string name);
    Task<string> GetErrorPage();
    Task<IEnumerable<string>> GetErrorPageNames();

    Task<bool> DeleteErrorPage(string name);

    Task<bool> DeleteEndPoint(string endPointId);
    Task<bool> DeleteEndPointQuery(string endPointId, string endPointQueryId);
    Task<bool> DeleteEndPointQueryView(string endPointId, string endPointQueryId, string endPointQueryViewId);

    Task<IDictionary<string, IEnumerable<string>>> GetEndPointPrefixes();
    Task<IEnumerable<string>> GetEndPointIds(IEnumerable<string> filters);
    Task<IEnumerable<string>> GetQueryIds(string endPointId);
    Task<IEnumerable<string>> GetViewIds(string endPointId, string queryId);

    Task<bool> SaveFolderStructure(Dictionary<string, List<string>> folderStructure);
    Task<string> GetFolderStructure();

    Task<bool> StoreEndPoint(DataLinqEndPoint endPoint);
    Task<bool> StoreEndPointQuery(DataLinqEndPointQuery endPointQuery);
    Task<bool> StoreEndPointQueryView(DataLinqEndPointQueryView endPointQueryView);
    Task<bool> StoreEndPointCss(string endPointId, string css);
    Task<bool> StoreViewCss(string id, string css);
    Task<bool> StoreEndPointJavascript(string endPointId, string css);
    Task<bool> StoreViewJs(string id, string js);
    Task<bool> StoreErrorPage(string name, string razorCode);
    Task<bool> StoreErrorPage(string razorCode);

    Task<bool> StoreCode(string id, string code);
    Task<bool> DeleteCode(string id);

    Task<bool> UpdateGitStatus(string id, bool upToDate);

    Task<string> EndPointCreator(string endPointId);

    Task<bool> DeleteLocalGitFolder();

    DataLinqCodeIdentity CurrentCodeIdentity { get; }
}
