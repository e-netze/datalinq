using E.DataLinq.Core.Models;
using E.DataLinq.Core.Services.Persistance.Abstraction;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services.VersionControl;

/// <summary>
/// Maps repository paths to DataLinq items.
/// Layout:
///   _folders.json
///   _errorpages/{name}.razor
///   {endpoint}/endpoint.json | endpoint.css | endpoint.js
///   {endpoint}/queries/{query}/query.json | query.sql
///   {endpoint}/queries/{query}/views/{view}/view.json | view.razor | view.css | view.js
/// Everything else (README.md, .gitattributes, ...) is an unmanaged file.
/// </summary>
public static class DataLinqGitPaths
{
    public const string FoldersFile = "_folders.json";
    public const string ErrorPagesDirectory = "_errorpages";
    public const string FoldersId = "_folders";
    public const string ErrorPageIdPrefix = "_errorpage@";
    public const string FileIdPrefix = "_file@";

    public static (string id, string kind) ToItem(string path)
    {
        path = Normalize(path);
        var p = path.Split('/');

        if (p.Length == 1 && p[0] == FoldersFile)
        {
            return (FoldersId, GitItemKinds.Folders);
        }

        if (p.Length == 2 && p[0] == ErrorPagesDirectory && p[1].EndsWith(".razor") && p[1].Length > ".razor".Length)
        {
            return (ErrorPageIdPrefix + p[1].Substring(0, p[1].Length - ".razor".Length), GitItemKinds.ErrorPage);
        }

        if (p.Length >= 2 && IsItemName(p[0]))
        {
            var ep = p[0];

            if (p.Length == 2)
            {
                switch (p[1])
                {
                    case "endpoint.json": return (ep, GitItemKinds.Endpoint);
                    case "endpoint.css": return ($"{ep}@_css", GitItemKinds.EndpointCss);
                    case "endpoint.js": return ($"{ep}@_js", GitItemKinds.EndpointJs);
                }
            }
            else if (p.Length == 4 && p[1] == "queries" && IsItemName(p[2]))
            {
                if (p[3] == "query.json" || p[3] == "query.sql")
                {
                    return ($"{ep}@{p[2]}", GitItemKinds.Query);
                }
            }
            else if (p.Length == 6 && p[1] == "queries" && IsItemName(p[2]) && p[3] == "views" && IsItemName(p[4]))
            {
                var viewId = $"{ep}@{p[2]}@{p[4]}";
                switch (p[5])
                {
                    case "view.json":
                    case "view.razor": return (viewId, GitItemKinds.View);
                    case "view.css": return ($"{viewId}@_css", GitItemKinds.ViewCss);
                    case "view.js": return ($"{viewId}@_js", GitItemKinds.ViewJs);
                }
            }
        }

        return (FileIdPrefix + path, GitItemKinds.File);
    }

    public static bool IsManaged(string path) => ToItem(path).kind != GitItemKinds.File;

    /// <summary>
    /// All repository paths that can belong to an item
    /// </summary>
    public static string[] ItemPaths(string id)
    {
        if (id == FoldersId)
        {
            return [FoldersFile];
        }

        if (id.StartsWith(ErrorPageIdPrefix))
        {
            return [$"{ErrorPagesDirectory}/{id.Substring(ErrorPageIdPrefix.Length)}.razor"];
        }

        if (id.StartsWith(FileIdPrefix))
        {
            return [id.Substring(FileIdPrefix.Length)];
        }

        var p = id.Split('@');
        return p.Length switch
        {
            1 => [$"{p[0]}/endpoint.json"],
            2 when p[1] == "_css" => [$"{p[0]}/endpoint.css"],
            2 when p[1] == "_js" => [$"{p[0]}/endpoint.js"],
            2 => [$"{p[0]}/queries/{p[1]}/query.json", $"{p[0]}/queries/{p[1]}/query.sql"],
            3 => [$"{p[0]}/queries/{p[1]}/views/{p[2]}/view.json", $"{p[0]}/queries/{p[1]}/views/{p[2]}/view.razor"],
            4 when p[3] == "_css" => [$"{p[0]}/queries/{p[1]}/views/{p[2]}/view.css"],
            4 when p[3] == "_js" => [$"{p[0]}/queries/{p[1]}/views/{p[2]}/view.js"],
            _ => Array.Empty<string>()
        };
    }

    public static string KindOf(string id) => ItemPaths(id).Select(p => ToItem(p).kind).FirstOrDefault() ?? GitItemKinds.File;

    public static string Normalize(string path) => (path ?? string.Empty).Replace('\\', '/').TrimStart('/');

    private static bool IsItemName(string name)
        => !string.IsNullOrEmpty(name) && !name.StartsWith("_") && !name.StartsWith(".");
}

/// <summary>
/// Synchronizes the DataLinq storage with the readable working tree of the repository.
/// The DataLinq storage is always the source of truth for uncommitted changes (export),
/// after git operations (pull, checkout, merge, discard) the working tree is imported.
/// </summary>
public class DataLinqGitMirror
{
    private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

    private readonly IPersistanceProviderService _persistance;
    private readonly ILogger _logger;

    public DataLinqGitMirror(IPersistanceProviderService persistance, ILogger logger)
    {
        _persistance = persistance;
        _logger = logger;
    }

    #region Snapshot

    /// <summary>
    /// The repository files (path => content) that represent the current DataLinq storage
    /// </summary>
    public async Task<Dictionary<string, string>> SnapshotStorageAsync()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var endPointId in await _persistance.GetEndPointIds(null))
        {
            var endPoint = await _persistance.GetEndPoint(endPointId);
            if (endPoint == null)
            {
                continue;
            }

            files[$"{endPointId}/endpoint.json"] = ToJson(JObject.FromObject(endPoint));
            AddText(files, $"{endPointId}/endpoint.css", await _persistance.GetEndPointCss(endPointId));
            AddText(files, $"{endPointId}/endpoint.js", await _persistance.GetEndPointJavascript(endPointId));

            foreach (var queryId in await _persistance.GetQueryIds(endPointId))
            {
                var query = await _persistance.GetEndPointQuery(endPointId, queryId);
                if (query == null)
                {
                    continue;
                }

                var queryPath = $"{endPointId}/queries/{queryId}";
                var queryJson = JObject.FromObject(query);
                queryJson.Remove("statement");
                queryJson.Remove("changed");

                files[$"{queryPath}/query.json"] = ToJson(queryJson);
                files[$"{queryPath}/query.sql"] = query.Statement ?? string.Empty;

                foreach (var viewId in await _persistance.GetViewIds(endPointId, queryId))
                {
                    var view = await _persistance.GetEndPointQueryView(endPointId, queryId, viewId);
                    if (view == null)
                    {
                        continue;
                    }

                    var viewPath = $"{queryPath}/views/{viewId}";
                    var viewJson = JObject.FromObject(view);
                    viewJson.Remove("code");
                    viewJson.Remove("changed");

                    files[$"{viewPath}/view.json"] = ToJson(viewJson);
                    files[$"{viewPath}/view.razor"] = view.Code ?? string.Empty;

                    var id = $"{endPointId}@{queryId}@{viewId}";
                    AddText(files, $"{viewPath}/view.css", await _persistance.GetViewCss(id));
                    AddText(files, $"{viewPath}/view.js", await _persistance.GetViewJs(id));
                }
            }
        }

        foreach (var errorPageName in await _persistance.GetErrorPageNames())
        {
            AddText(files, $"{DataLinqGitPaths.ErrorPagesDirectory}/{errorPageName}.razor", await _persistance.GetErrorPage(errorPageName));
        }

        var folders = ParseFolders(await _persistance.GetFolderStructure());
        if (folders != null && folders.Count > 0)
        {
            files[DataLinqGitPaths.FoldersFile] = ToJson(JObject.FromObject(folders));
        }

        return files;
    }

    /// <summary>
    /// All managed files of the working tree (path => content)
    /// </summary>
    public static Dictionary<string, string> ReadWorkingTree(string root)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var rootInfo = new DirectoryInfo(root);

        if (!rootInfo.Exists)
        {
            return files;
        }

        foreach (var fi in EnumerateFiles(rootInfo))
        {
            var path = DataLinqGitPaths.Normalize(Path.GetRelativePath(rootInfo.FullName, fi.FullName));
            if (DataLinqGitPaths.IsManaged(path))
            {
                files[path] = File.ReadAllText(fi.FullName, Encoding.UTF8);
            }
        }

        return files;
    }

    /// <summary>
    /// Item ids whose files differ between storage and working tree
    /// </summary>
    public static List<string> DiffItems(Dictionary<string, string> storage, Dictionary<string, string> tree)
    {
        var items = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in storage.Keys.Union(tree.Keys))
        {
            storage.TryGetValue(path, out var storageContent);
            tree.TryGetValue(path, out var treeContent);

            if (!string.Equals(storageContent, treeContent, StringComparison.Ordinal))
            {
                items.Add(DataLinqGitPaths.ToItem(path).id);
            }
        }

        return items.OrderBy(i => i).ToList();
    }

    #endregion

    #region Export (storage => working tree)

    public static void Export(string root, Dictionary<string, string> storage, IEnumerable<string> itemIds)
    {
        foreach (var itemId in itemIds)
        {
            foreach (var path in DataLinqGitPaths.ItemPaths(itemId))
            {
                var fullPath = FullPath(root, path);

                if (storage.TryGetValue(path, out var content))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                    File.WriteAllText(fullPath, content, Utf8NoBom);
                }
                else if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    DeleteEmptyDirectories(root, Path.GetDirectoryName(fullPath));
                }
            }
        }
    }

    #endregion

    #region Import (working tree => storage)

    public class ImportResult
    {
        public List<string> Updated { get; } = new();
        public List<string> Deleted { get; } = new();
        public List<string> Errors { get; } = new();
        public HashSet<string> FailedIds { get; } = new(StringComparer.Ordinal);
    }

    public async Task<ImportResult> ImportAsync(Dictionary<string, string> tree, IEnumerable<string> itemIds)
    {
        var result = new ImportResult();
        var items = itemIds
            .Select(id => (id, kind: DataLinqGitPaths.KindOf(id)))
            .Where(i => i.kind != GitItemKinds.File)
            .ToList();

        bool InTree(string id) => DataLinqGitPaths.ItemPaths(id).Take(1).Any(p => tree.ContainsKey(p));
        string TreeFile(string path) => tree.TryGetValue(path, out var content) ? content : null;

        var upserts = items.Where(i => InTree(i.id) || IsTextItem(i.kind)).ToList();
        var deletes = items.Where(i => !InTree(i.id) && !IsTextItem(i.kind)).ToList();

        // create/update: endpoints => queries => views => everything else
        foreach (var kind in new[] { GitItemKinds.Endpoint, GitItemKinds.Query, GitItemKinds.View })
        {
            foreach (var item in upserts.Where(i => i.kind == kind))
            {
                await Run(result, item.id, false, () => ImportItemAsync(item.id, item.kind, TreeFile));
            }
        }
        foreach (var item in upserts.Where(i => !new[] { GitItemKinds.Endpoint, GitItemKinds.Query, GitItemKinds.View }.Contains(i.kind)))
        {
            var deleted = !InTree(item.id);
            await Run(result, item.id, deleted && item.kind == GitItemKinds.ErrorPage, () => ImportItemAsync(item.id, item.kind, TreeFile));
        }

        // delete: views => queries => endpoints
        foreach (var kind in new[] { GitItemKinds.View, GitItemKinds.Query, GitItemKinds.Endpoint })
        {
            foreach (var item in deletes.Where(i => i.kind == kind))
            {
                await Run(result, item.id, true, () => DeleteItemAsync(item.id, item.kind));
            }
        }

        return result;
    }

    private async Task Run(ImportResult result, string id, bool isDelete, Func<Task<bool>> action)
    {
        try
        {
            if (await action())
            {
                (isDelete ? result.Deleted : result.Updated).Add(id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Git import of {id} failed", id);
            result.Errors.Add($"{id}: {ex.Message}");
            result.FailedIds.Add(id);
        }
    }

    private async Task<bool> ImportItemAsync(string id, string kind, Func<string, string> treeFile)
    {
        var paths = DataLinqGitPaths.ItemPaths(id);
        var p = id.Split('@');

        switch (kind)
        {
            case GitItemKinds.Endpoint:
                {
                    var endPoint = Deserialize<DataLinqEndPoint>(treeFile(paths[0]), paths[0]);
                    endPoint.Id = p[0];
                    return await _persistance.StoreEndPoint(endPoint);
                }
            case GitItemKinds.Query:
                {
                    await EnsureEndPointExists(p[0]);

                    var existing = await _persistance.GetEndPointQuery(p[0], p[1]);
                    var query = Deserialize<DataLinqEndPointQuery>(treeFile(paths[0]), paths[0]);
                    query.EndPointId = p[0];
                    query.QueryId = p[1];
                    query.Statement = treeFile(paths[1]) ?? string.Empty;
                    query.Created = query.Created == default ? existing?.Created ?? DateTime.UtcNow : query.Created;
                    query.Changed = DateTime.UtcNow;
                    return await _persistance.StoreEndPointQuery(query);
                }
            case GitItemKinds.View:
                {
                    await EnsureEndPointExists(p[0]);
                    if (await _persistance.GetEndPointQuery(p[0], p[1]) == null)
                    {
                        throw new Exception($"Query {p[0]}@{p[1]} does not exist");
                    }

                    var existing = await _persistance.GetEndPointQueryView(p[0], p[1], p[2]);
                    var view = Deserialize<DataLinqEndPointQueryView>(treeFile(paths[0]), paths[0]);
                    view.EndPointId = p[0];
                    view.QueryId = p[1];
                    view.ViewId = p[2];
                    view.Code = treeFile(paths[1]) ?? string.Empty;
                    view.Created = view.Created == default ? existing?.Created ?? DateTime.UtcNow : view.Created;
                    view.Changed = DateTime.UtcNow;  // invalidates the razor cache
                    return await _persistance.StoreEndPointQueryView(view);
                }
            case GitItemKinds.EndpointCss:
                await EnsureEndPointExists(p[0]);
                return await _persistance.StoreEndPointCss(p[0], treeFile(paths[0]) ?? string.Empty);
            case GitItemKinds.EndpointJs:
                await EnsureEndPointExists(p[0]);
                return await _persistance.StoreEndPointJavascript(p[0], treeFile(paths[0]) ?? string.Empty);
            case GitItemKinds.ViewCss:
            case GitItemKinds.ViewJs:
                {
                    var viewId = string.Join("@", p.Take(3));
                    if (await _persistance.GetEndPointQueryView(p[0], p[1], p[2]) == null)
                    {
                        // view does not exist (anymore) => nothing to store
                        return false;
                    }

                    return kind == GitItemKinds.ViewCss
                        ? await _persistance.StoreViewCss(viewId, treeFile(paths[0]) ?? string.Empty)
                        : await _persistance.StoreViewJs(viewId, treeFile(paths[0]) ?? string.Empty);
                }
            case GitItemKinds.ErrorPage:
                {
                    var name = id.Substring(DataLinqGitPaths.ErrorPageIdPrefix.Length);
                    var content = treeFile(paths[0]);

                    if (content == null && !DataLinqErrorPage.GlobalName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        return await _persistance.DeleteErrorPage(name);
                    }

                    return await _persistance.StoreErrorPage(name, content ?? string.Empty);
                }
            case GitItemKinds.Folders:
                {
                    var content = treeFile(paths[0]);
                    var folders = content == null
                        ? new Dictionary<string, List<string>>()
                        : JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(content) ?? new Dictionary<string, List<string>>();
                    return await _persistance.SaveFolderStructure(folders);
                }
        }

        return false;
    }

    private async Task<bool> DeleteItemAsync(string id, string kind)
    {
        var p = id.Split('@');

        switch (kind)
        {
            case GitItemKinds.Endpoint:
                if (await _persistance.GetEndPoint(p[0]) == null)
                {
                    return false;
                }
                return await _persistance.DeleteEndPoint(p[0]);
            case GitItemKinds.Query:
                if (await _persistance.GetEndPointQuery(p[0], p[1]) == null)
                {
                    return false;
                }
                return await _persistance.DeleteEndPointQuery(p[0], p[1]);
            case GitItemKinds.View:
                if (await _persistance.GetEndPointQueryView(p[0], p[1], p[2]) == null)
                {
                    return false;
                }
                // the view css/js files are not removed by the persistance provider
                await _persistance.StoreViewCss(id, string.Empty);
                await _persistance.StoreViewJs(id, string.Empty);
                return await _persistance.DeleteEndPointQueryView(p[0], p[1], p[2]);
        }

        return false;
    }

    private async Task EnsureEndPointExists(string endPointId)
    {
        if (await _persistance.GetEndPoint(endPointId) == null)
        {
            throw new Exception($"Endpoint {endPointId} does not exist");
        }
    }

    private static bool IsTextItem(string kind) =>
        kind is GitItemKinds.EndpointCss or GitItemKinds.EndpointJs or GitItemKinds.ViewCss or GitItemKinds.ViewJs
             or GitItemKinds.ErrorPage or GitItemKinds.Folders;

    private static T Deserialize<T>(string json, string path)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new Exception($"{path} is empty");
        }

        try
        {
            return JsonConvert.DeserializeObject<T>(json) ?? throw new Exception("null");
        }
        catch (Exception ex)
        {
            throw new Exception($"{path} is not valid json: {ex.Message}", ex);
        }
    }

    #endregion

    #region Helper

    public static string ToJson(JToken token)
    {
        var sw = new StringWriter() { NewLine = "\n" };
        using (var jw = new JsonTextWriter(sw) { Formatting = Formatting.Indented, Indentation = 2 })
        {
            token.WriteTo(jw);
        }
        return sw.ToString() + "\n";
    }

    private static void AddText(Dictionary<string, string> files, string path, string content)
    {
        if (!string.IsNullOrEmpty(content))
        {
            files[path] = content;
        }
    }

    private static Dictionary<string, List<string>> ParseFolders(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
        {
            return null;
        }

        try
        {
            return JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);
        }
        catch
        {
            return null;
        }
    }

    public static string FullPath(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, DataLinqGitPaths.Normalize(path)));

        if (!fullPath.StartsWith(fullRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception($"Invalid repository path {path}");
        }

        return fullPath;
    }

    private static IEnumerable<FileInfo> EnumerateFiles(DirectoryInfo directory)
    {
        foreach (var fi in directory.GetFiles())
        {
            yield return fi;
        }

        foreach (var di in directory.GetDirectories())
        {
            if (di.Name == ".git" || di.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            foreach (var fi in EnumerateFiles(di))
            {
                yield return fi;
            }
        }
    }

    public static void DeleteEmptyDirectories(string root, string directory)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);

        while (!string.IsNullOrEmpty(directory))
        {
            var full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
            if (full.Length <= fullRoot.Length || !Directory.Exists(full) || Directory.EnumerateFileSystemEntries(full).Any())
            {
                return;
            }

            Directory.Delete(full);
            directory = Path.GetDirectoryName(full);
        }
    }

    #endregion
}
