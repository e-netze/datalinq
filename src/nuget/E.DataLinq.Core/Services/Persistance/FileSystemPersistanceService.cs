using E.DataLinq.Core.Extensions;
using E.DataLinq.Core.Models;
using E.DataLinq.Core.Models.Authentication;
using E.DataLinq.Core.Services.Abstraction;
using E.DataLinq.Core.Services.Crypto.Abstraction;
using E.DataLinq.Core.Services.Persistance.Abstraction;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace E.DataLinq.Core.Services.Persistance;

public class FileSystemPersistanceService : IPersistanceProviderService
{
    private readonly ICryptoService _crypto;
    private readonly PersistanceProviderServiceOptions _options;
    private readonly string _storagePath;


    public FileSystemPersistanceService(ICryptoService crypto,
                                        IOptionsMonitor<PersistanceProviderServiceOptions> optionsMonitor,
                                        IDataLinqCodeIdentityService _identitySerice = null)
    {
        _crypto = crypto;
        _options = optionsMonitor.CurrentValue;
        _storagePath = _options.ConnectionString;

        this.CurrentCodeIdentity = _identitySerice?.CurrentIdentity();
    }

    #region IPersistanceProviderService

    public DataLinqCodeIdentity CurrentCodeIdentity { get; }

    async public Task<DataLinqEndPoint> GetEndPoint(string endPointId)
    {
        FileInfo fi = new FileInfo(EndPointBlobPath(endPointId));

        if (fi.Exists)
        {
            var endpoint = JsonConvert.DeserializeObject<DataLinqEndPoint>(await File.ReadAllTextAsync(fi.FullName))
                .DecryptSecureProperties(_crypto);

            if (endpoint is null)
            {
                throw new Exception($"Can't deserialize endpoint: {endPointId}");
            }

            return endpoint;
        }

        return null;
    }

    async public Task<DataLinqEndPointQuery> GetEndPointQuery(string endPointId, string endPointQueryId)
    {
        FileInfo fi = new FileInfo(EndPointQueryBlobPath(endPointId, endPointQueryId));

        if (fi.Exists)
        {
            var query = JsonConvert.DeserializeObject<DataLinqEndPointQuery>(await File.ReadAllTextAsync(fi.FullName))
                .DecryptSecureProperties(_crypto);

            if (query is null)
            {
                throw new Exception($"Can't deserialize query: {endPointId}@{endPointQueryId}");
            }
            // EndPointId is not stored in File [JsonIgnore]
            query.EndPointId = endPointId;

            return query;
        }

        return null;
    }

    async public Task<DataLinqEndPointQueryView> GetEndPointQueryView(string endPointId, string endPointQueryId, string endPointQueryViewId)
    {
        FileInfo fi = new FileInfo(EndPointQueryViewPath(endPointId, endPointQueryId, endPointQueryViewId));

        if (fi.Exists)
        {
            var view = JsonConvert.DeserializeObject<DataLinqEndPointQueryView>(await File.ReadAllTextAsync(fi.FullName))
                .DecryptSecureProperties(_crypto);

            if (view is null)
            {
                throw new Exception($"Can't deserialize view: {endPointId}@{endPointQueryId}@{endPointQueryViewId}");
            }
            // EndPointId, QueryId is not stored in File [JsonIgnore]
            view.EndPointId = endPointId;
            view.QueryId = endPointQueryId;

            return view;
        }

        return null;
    }

    async public Task<string> GetEndPointCss(string endPointId)
    {
        FileInfo fi = new FileInfo(EndPointCssBloblPath(endPointId));

        if (fi.Exists)
        {
            return await File.ReadAllTextAsync(fi.FullName);
        }

        return string.Empty;
    }

    async public Task<string> GetEndPointJavascript(string endPointId)
    {
        FileInfo fi = new FileInfo(EndPointJavascriptBloblPath(endPointId));

        if (fi.Exists)
        {
            return await File.ReadAllTextAsync(fi.FullName);
        }

        return string.Empty;
    }

    async public Task<IDictionary<string, IEnumerable<string>>> GetEndPointPrefixes()
    {
        var result = new Dictionary<string, IEnumerable<string>>();
        var endPointIds = await GetEndPointIds(null);

        foreach (var endPointId in endPointIds)
        {
            var prefix = endPointId.Split('-').First();

            if (String.IsNullOrEmpty(prefix))
            {
                continue;
            }

            if (!result.ContainsKey(prefix))
            {
                result.Add(prefix, new List<string>());
            }

            if (prefix != endPointId)
            {
                ((List<string>)result[prefix]).Add(endPointId.Substring(prefix.Length + 1));
            }
        }

        return result;
    }

    public Task<IEnumerable<string>> GetEndPointIds(IEnumerable<string> filters)
    {
        var Ids = new List<string>();
        var di = new DirectoryInfo(_storagePath);

        filters = filters?
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => f.Trim());

        var applyFilters = filters != null && filters.Count() > 0;

        if (di.Exists)
        {
            foreach (var endPointDir in di.GetDirectories())
            {
                if (endPointDir.Name.StartsWith("_"))  // hidden endpoints
                {
                    continue;
                }

                string id = endPointDir.Name;
                FileInfo fi = new FileInfo(EndPointBlobPath(endPointDir.Name));

                if (fi.Exists)
                {
                    if (applyFilters && !filters.Contains(id) && filters.Where(f => id.StartsWith($"{f}-")).Count() == 0)
                    {
                        continue;
                    }

                    Ids.Add(endPointDir.Name);
                }
            }
        }

        return Task.FromResult<IEnumerable<string>>(Ids.OrderBy(id => id));
    }

    public Task<IEnumerable<string>> GetQueryIds(string endPointId)
    {
        var Ids = new List<string>();
        var di = new DirectoryInfo(Path.Combine(_storagePath, endPointId, "queries"));

        if (di.Exists)
        {
            foreach (var fi in di.GetFiles("*.blb"))
            {
                if (fi.Name.StartsWith("_"))
                {
                    continue;
                }

                Ids.Add(fi.Name.Substring(0, fi.Name.LastIndexOf(".")));
            }
        }

        return Task.FromResult<IEnumerable<string>>(Ids.OrderBy(id => id));
    }

    public Task<IEnumerable<string>> GetViewIds(string endPointId, string queryId)
    {
        var Ids = new List<string>();
        var di = new DirectoryInfo(Path.Combine(_storagePath, endPointId, "queries", $"{queryId}-views"));

        if (di.Exists)
        {
            foreach (var fi in di.GetFiles("*.blb"))
            {
                if (fi.Name.StartsWith("_"))
                {
                    continue;
                }

                Ids.Add(fi.Name.Substring(0, fi.Name.LastIndexOf(".")));
            }
        }

        return Task.FromResult<IEnumerable<string>>(Ids.OrderBy(id => id));
    }

    async public Task<bool> StoreEndPoint(DataLinqEndPoint endPoint)
    {
        FileInfo fi = new FileInfo(EndPointBlobPath(endPoint.Id));

        if (!fi.Directory.Exists)
        {
            // New EndPoint
            fi.Directory.Create();

            await CreateEndpointIndex(endPoint.Id);
        }

        await File.WriteAllTextAsync(fi.FullName,
            JsonConvert.SerializeObject(endPoint.EncryptSecureProperties(_crypto, _options.SecureStringEncryptionLevel)));

        return true;
    }

    async public Task<bool> StoreEndPointQuery(DataLinqEndPointQuery endPointQuery)
    {
        FileInfo fi = new FileInfo(EndPointQueryBlobPath(endPointQuery.EndPointId, endPointQuery.QueryId));

        if (!fi.Directory.Exists)
        {
            fi.Directory.Create();
        }

        await File.WriteAllTextAsync(fi.FullName,
            JsonConvert.SerializeObject(endPointQuery.EncryptSecureProperties(_crypto, _options.SecureStringEncryptionLevel)));

        return true;
    }

    async public Task<bool> StoreEndPointQueryView(DataLinqEndPointQueryView endPointQueryView)
    {
        FileInfo fi = new FileInfo(EndPointQueryViewPath(endPointQueryView.EndPointId, endPointQueryView.QueryId, endPointQueryView.ViewId));

        if (!fi.Directory.Exists)
        {
            fi.Directory.Create();
        }

        await File.WriteAllTextAsync(fi.FullName,
            JsonConvert.SerializeObject(endPointQueryView.EncryptSecureProperties(_crypto, _options.SecureStringEncryptionLevel)));

        return true;
    }

    async public Task<bool> StoreEndPointCss(string endPointId, string css)
    {
        FileInfo fi = new FileInfo(EndPointCssBloblPath(endPointId));

        await File.WriteAllTextAsync(fi.FullName, css);

        return true;
    }

    async public Task<bool> StoreEndPointJavascript(string endPointId, string js)
    {
        FileInfo fi = new FileInfo(EndPointJavascriptBloblPath(endPointId));

        await File.WriteAllTextAsync(fi.FullName, js);

        return true;
    }

    async public Task<bool> DeleteEndPoint(string endPointId)
    {
        await DeleteEndpointIndex(endPointId);

        FileInfo fi = new FileInfo(EndPointBlobPath(endPointId));

        if (!fi.Directory.Exists)
        {
            throw new ArgumentException($"Endpoint {endPointId} not exists");
        }

        fi.Directory.Delete(true);

        return true;
    }

    public Task<bool> DeleteEndPointQuery(string endPointId, string endPointQueryId)
    {
        FileInfo fi = new FileInfo(EndPointQueryBlobPath(endPointId, endPointQueryId));
        DirectoryInfo di = new DirectoryInfo(Path.Combine(fi.Directory.FullName, $"{endPointQueryId}-views"));

        bool found = false;
        if (di.Exists)
        {
            found = true;
            di.Delete(true);
        }

        if (fi.Exists)
        {
            found = true;
            fi.Delete();
        }

        if (!found)
        {
            throw new ArgumentException($"Query {endPointId}@{endPointQueryId} not exists");
        }

        return Task.FromResult(true);
    }

    public Task<bool> DeleteEndPointQueryView(string endPointId, string endPointQueryId, string endPointQueryViewId)
    {
        FileInfo fi = new FileInfo(EndPointQueryViewPath(endPointId, endPointQueryId, endPointQueryViewId));

        if (!fi.Exists)
        {
            throw new ArgumentException($"View {endPointId}@{endPointQueryId}@{endPointQueryViewId} not exists");
        }

        fi.Delete();

        return Task.FromResult(true);
    }

    async public Task<string> EndPointCreator(string endPointId)
    {
        var userIndexFile = new FileInfo(Path.Combine(_storagePath, "_index", "endpoints", $"{endPointId}.idx"));
        if (userIndexFile.Exists)
        {
            return (await File.ReadAllTextAsync(userIndexFile.FullName)).Trim();
        }

        return String.Empty;
    }

    #endregion

    #region Helper

    private string EndPointBlobPath(string endPointId)
    {
        if (!endPointId.IsValidDataLinqRouteId())
        {
            throw new Exception($"Invalid endpoint id {endPointId}");
        }

        return Path.Combine(_storagePath, endPointId, $"{endPointId}.blb");
    }

    private string EndPointQueryBlobPath(string endPointId, string queryId)
    {
        if (!endPointId.IsValidDataLinqRouteId())
        {
            throw new Exception($"Invalid endpoint id {endPointId}");
        }

        if (!queryId.IsValidDataLinqRouteId())
        {
            throw new Exception($"Invalid query id {queryId}");
        }

        return Path.Combine(_storagePath, endPointId, "queries", $"{queryId}.blb");
    }

    private string EndPointQueryViewPath(string endPointId, string queryId, string viewId)
    {
        if (!endPointId.IsValidDataLinqRouteId())
        {
            throw new Exception($"Invalid endpoint id {endPointId}");
        }

        if (!queryId.IsValidDataLinqRouteId())
        {
            throw new Exception($"Invalid query id {queryId}");
        }

        if (!viewId.IsValidDataLinqRouteId())
        {
            throw new Exception($"Invalid view id {viewId}");
        }

        return Path.Combine(_storagePath, endPointId, "queries", $"{queryId}-views", $"{viewId}.blb");
    }

    private string EndPointCssBloblPath(string endPointId)
    {
        if (!endPointId.IsValidDataLinqRouteId())
        {
            throw new Exception($"Invalid endpoint id {endPointId}");
        }

        return Path.Combine(_storagePath, endPointId, "_css.blb");
    }

    private string EndPointJavascriptBloblPath(string endPointId)
    {
        if (!endPointId.IsValidDataLinqRouteId())
        {
            throw new Exception($"Invalid endpoint id {endPointId}");
        }

        return Path.Combine(_storagePath, endPointId, "_js.blb");
    }

    async private Task CreateEndpointIndex(string endPointId)
    {
        if (String.IsNullOrEmpty(CurrentCodeIdentity?.Name))
        {
            throw new Exception("Not authorized to create an endpoint index");
        }

        var indexDirectory = new DirectoryInfo(Path.Combine(_storagePath, "_index"));

        if (!indexDirectory.Exists)
        {
            indexDirectory.Create();
        }

        var endPointIndexDirectory = new DirectoryInfo(Path.Combine(_storagePath, "_index", "endpoints"));
        if (!endPointIndexDirectory.Exists)
        {
            endPointIndexDirectory.Create();
        }

        var userIndexDirectory = new DirectoryInfo(Path.Combine(_storagePath, "_index", CurrentCodeIdentity.Name.Username2StorageDirectory()));
        if (!userIndexDirectory.Exists)
        {
            userIndexDirectory.Create();
        }

        await File.WriteAllTextAsync(Path.Combine(endPointIndexDirectory.FullName, $"{endPointId}.idx"),
                                     CurrentCodeIdentity.Name);
        await File.WriteAllTextAsync(Path.Combine(userIndexDirectory.FullName, $"{endPointId}.idx"),
                                     String.Empty);
    }

    async private Task DeleteEndpointIndex(string endPointId)
    {
        var endPointIndexFile = new FileInfo(Path.Combine(_storagePath, "_index", "endpoints", $"{endPointId}.idx"));
        if (endPointIndexFile.Exists)
        {
            var userName = (await File.ReadAllTextAsync(endPointIndexFile.FullName)).Trim();

            var userIndexFileDirectory = new DirectoryInfo(Path.Combine(_storagePath, "_index", userName.Username2StorageDirectory()));
            if (userIndexFileDirectory.Exists)
            {
                var userIndexFile = new FileInfo(Path.Combine(_storagePath, "_index", userName.Username2StorageDirectory(), $"{endPointId}.idx"));

                if (userIndexFile.Exists)
                {
                    userIndexFile.Delete();
                }

                // delete empty folders
                if (userIndexFileDirectory.GetFiles().Length == 0 &&
                    userIndexFileDirectory.GetDirectories().Length == 0)
                {
                    userIndexFileDirectory.Delete();
                }
            }

            endPointIndexFile.Delete();
        }
    }

    #endregion
}
