using E.DataLinq.Core.Services.Persistance;
using E.DataLinq.Core.Services.Persistance.Abstraction;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services.VersionControl;

/// <summary>
/// Git version control for the DataLinq storage (one shared working copy per instance).
/// All operations are serialized by a process wide lock.
/// </summary>
public class DataLinqGitService
{
    private const string OriginName = "origin";
    private const string MergeInfoFile = "DATALINQ_MERGE";
    private const string MergeSnapshotFile = "DATALINQ_MERGE_SNAPSHOT";
    private const string ImportErrorsFile = "DATALINQ_IMPORT_ERRORS";
    private const int MaxDiffTextLength = 1024 * 1024;

    private static readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private readonly DataLinqVersionControlOptions _options;
    private readonly PersistanceProviderServiceOptions _persistanceOptions;
    private readonly DataLinqGitMirror _mirror;
    private readonly ILogger<DataLinqGitService> _logger;

    public DataLinqGitService(IOptions<DataLinqVersionControlOptions> options,
                              IOptionsMonitor<PersistanceProviderServiceOptions> persistanceOptions,
                              IPersistanceProviderService persistance,
                              ILogger<DataLinqGitService> logger)
    {
        _options = options.Value;
        _persistanceOptions = persistanceOptions.CurrentValue;
        _logger = logger;
        _mirror = new DataLinqGitMirror(persistance, logger);
    }

    public bool IsEnabled => _options.UseVersionControl;

    private string RepoPath => Path.GetFullPath(_options.LocalRepositoryPath);
    private string MainBranch => string.IsNullOrWhiteSpace(_options.MainBranch) ? "main" : _options.MainBranch.Trim();

    #region Status

    public Task<GitResult> StatusAsync(bool fetch) => RunAsync(async () =>
    {
        if (!Repository.IsValid(RepoPath))
        {
            return new GitResult { Success = true, Message = "Not connected", Status = NotConnectedStatus() };
        }

        using var repo = OpenRepository();

        string fetchError = null;
        if (fetch)
        {
            try
            {
                Fetch(repo);
            }
            catch (Exception ex)
            {
                fetchError = ToResult(ex).Message;
            }
        }

        var result = GitResult.Ok(fetchError == null ? "ok" : $"Fetch failed: {fetchError}");
        result.Status = await BuildStatusAsync(repo, sync: true);
        return result;
    });

    #endregion

    #region Connect

    public Task<GitResult> ConnectAsync(string mode, string author) => RunAsync(async () =>
    {
        if (Repository.IsValid(RepoPath))
        {
            using var existing = OpenRepository();
            return WithStatus(GitResult.Ok("Repository is already connected"), await BuildStatusAsync(existing, true));
        }

        var guard = CheckLocalPath();
        if (guard != null)
        {
            return guard;
        }

        var remoteRefs = Repository.ListRemoteReferences(_options.RemoteUrl, CredentialsHandler()).ToList();
        var remoteIsEmpty = !remoteRefs.Any(r => r.CanonicalName.StartsWith("refs/heads/"));

        if (remoteIsEmpty)
        {
            Directory.CreateDirectory(RepoPath);
            Repository.Init(RepoPath);

            using var repo = new Repository(RepoPath);
            repo.Config.Set("core.autocrlf", false);
            repo.Refs.UpdateTarget("HEAD", $"refs/heads/{MainBranch}");
            repo.Network.Remotes.Add(OriginName, _options.RemoteUrl);

            WriteRepositoryFiles(repo);
            await ExportAsync(repo);

            Commands.Stage(repo, "*");
            var signature = Signature(author);
            repo.Commit("Initial DataLinq export", signature, signature);

            Push(repo);

            return WithStatus(GitResult.Ok("Repository initialized and DataLinq content pushed to the empty remote repository"),
                              await BuildStatusAsync(repo, true));
        }

        if (mode != "import" && mode != "merge-local")
        {
            return GitResult.Fail(GitErrorCodes.RemoteNotEmpty,
                "The remote repository already contains content. Choose how to connect.");
        }

        var cloneOptions = new CloneOptions() { Checkout = true };
        cloneOptions.FetchOptions.CredentialsProvider = CredentialsHandler();
        if (remoteRefs.Any(r => r.CanonicalName == $"refs/heads/{MainBranch}"))
        {
            cloneOptions.BranchName = MainBranch;
        }

        Repository.Clone(_options.RemoteUrl, RepoPath, cloneOptions);

        using (var repo = new Repository(RepoPath))
        {
            repo.Config.Set("core.autocrlf", false);

            if (mode == "merge-local")
            {
                var storage = await _mirror.SnapshotStorageAsync();
                var tree = DataLinqGitMirror.ReadWorkingTree(repo.Info.WorkingDirectory);
                var storagePaths = storage.Keys.Select(p => DataLinqGitPaths.ToItem(p).id).ToHashSet();

                // local items win, items that only exist in the repository are kept
                var localItems = DataLinqGitMirror.DiffItems(storage, tree).Where(storagePaths.Contains).ToList();
                DataLinqGitMirror.Export(repo.Info.WorkingDirectory, storage, localItems);
                WriteRepositoryFiles(repo);

                Commands.Stage(repo, "*");
                if (repo.RetrieveStatus().IsDirty)
                {
                    var signature = Signature(author);
                    repo.Commit("Merge local DataLinq content", signature, signature);
                    Push(repo);
                }
            }

            var import = await ImportAsync(repo);
            return WithStatus(WithImport(GitResult.Ok(mode == "import"
                    ? "Repository connected and its content imported into DataLinq"
                    : "Repository connected, local content merged and pushed"), import),
                await BuildStatusAsync(repo, true));
        }
    });

    public Task<GitResult> DisconnectAsync() => RunAsync(() =>
    {
        if (!Repository.IsValid(RepoPath))
        {
            return Task.FromResult(GitResult.Fail(GitErrorCodes.NotConnected, "Repository is not connected"));
        }

        DeleteDirectory(RepoPath);
        return Task.FromResult(new GitResult { Success = true, Message = "Local working copy removed. DataLinq content was not changed.", Status = NotConnectedStatus() });
    });

    #endregion

    #region Commit / Push / Pull

    public Task<GitResult> CommitAsync(string message, string[] ids, bool push, string author) => RunConnectedAsync(async repo =>
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return GitResult.Fail(GitErrorCodes.InvalidArgument, "A commit message is required");
        }

        if (IsMerging(repo))
        {
            return GitResult.Fail(GitErrorCodes.MergeInProgress, "A merge is in progress. Resolve the conflicts and complete or abort the merge.");
        }

        var status = await BuildStatusAsync(repo, true);
        var selected = status.Changes
            .Where(c => ids == null || ids.Length == 0 || ids.Contains(c.Id))
            .ToList();

        var paths = selected.SelectMany(c => c.Paths).Distinct().ToList();
        if (paths.Count == 0)
        {
            return WithStatus(GitResult.Fail(GitErrorCodes.NothingToCommit, "There is nothing to commit"), status);
        }

        Commands.Stage(repo, paths);
        var signature = Signature(author);
        var commit = repo.Commit(message.Trim(), signature, signature);

        var result = GitResult.Ok($"Committed {selected.Count} item(s) ({commit.Sha.Substring(0, 7)})");

        if (push)
        {
            var pushResult = TryPush(repo);
            result = pushResult.Success
                ? GitResult.Ok($"{result.Message} and pushed to {pushResult.Data}")
                : GitResult.Fail(pushResult.ErrorCode, $"{result.Message}, but push failed: {pushResult.Message}");
        }

        return WithStatus(result, await BuildStatusAsync(repo, true));
    });

    public Task<GitResult> PushAsync() => RunConnectedAsync(async repo =>
    {
        if (IsMerging(repo))
        {
            return GitResult.Fail(GitErrorCodes.MergeInProgress, "A merge is in progress. Complete or abort the merge first.");
        }

        var result = TryPush(repo);
        if (result.Success)
        {
            result.Message = $"Pushed to {result.Data}";
            result.Data = null;
        }

        return WithStatus(result, await BuildStatusAsync(repo, true));
    });

    public Task<GitResult> FetchAsync() => RunConnectedAsync(async repo =>
    {
        Fetch(repo);
        return WithStatus(GitResult.Ok("Fetched"), await BuildStatusAsync(repo, true));
    });

    public Task<GitResult> PullAsync(string author) => RunConnectedAsync(async repo =>
    {
        if (IsMerging(repo))
        {
            return GitResult.Fail(GitErrorCodes.MergeInProgress, "A merge is in progress. Complete or abort the merge first.");
        }

        var storage = await ExportAsync(repo);
        Fetch(repo);

        var head = repo.Head;
        var upstream = EnsureUpstream(repo, head);
        if (upstream?.Tip == null)
        {
            return WithStatus(GitResult.Ok($"Branch '{head.FriendlyName}' is not published yet. Nothing to pull."), await BuildStatusAsync(repo, true));
        }

        var mergeResult = repo.Merge(upstream, Signature(author), MergeOptions());

        if (mergeResult.Status == MergeStatus.Conflicts)
        {
            StartMerge(repo, new GitMergeInfo { Kind = "pull", Source = upstream.FriendlyName, Target = head.FriendlyName }, storage);
            return WithStatus(GitResult.Fail(GitErrorCodes.Conflicts,
                    "Pull resulted in conflicts. Choose 'Keep mine' or 'Take theirs' for each conflicting item, then complete the merge."),
                await BuildStatusAsync(repo, false));
        }

        var import = await ImportAsync(repo);
        var message = mergeResult.Status switch
        {
            MergeStatus.UpToDate => "Already up to date",
            MergeStatus.FastForward => "Pulled (fast forward)",
            _ => "Pulled and merged"
        };

        return WithStatus(WithImport(GitResult.Ok(message), import), await BuildStatusAsync(repo, true));
    });

    #endregion

    #region Branches

    public Task<GitResult> BranchesAsync() => RunConnectedAsync(repo =>
    {
        var branches = new Dictionary<string, GitBranchInfo>(StringComparer.Ordinal);
        var current = repo.Head.FriendlyName;

        foreach (var branch in repo.Branches)
        {
            string name;
            if (branch.IsRemote)
            {
                if (branch.RemoteName != OriginName || branch.CanonicalName.EndsWith("/HEAD"))
                {
                    continue;
                }
                name = branch.FriendlyName.Substring(OriginName.Length + 1);
            }
            else
            {
                name = branch.FriendlyName;
            }

            if (!branches.TryGetValue(name, out var info))
            {
                branches[name] = info = new GitBranchInfo { Name = name, IsMain = name == MainBranch, IsCurrent = name == current };
            }

            if (branch.IsRemote)
            {
                info.IsRemote = true;
            }
            else
            {
                info.IsLocal = true;
                info.Ahead = branch.TrackingDetails?.AheadBy ?? 0;
                info.Behind = branch.TrackingDetails?.BehindBy ?? 0;
            }

            var when = branch.Tip?.Committer.When;
            if (when != null && (info.LastCommit == null || when > info.LastCommit))
            {
                info.LastCommit = when;
            }
        }

        var list = branches.Values
            .OrderByDescending(b => b.IsMain)
            .ThenBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(GitResult.Ok($"{list.Count} branches", list));
    });

    public Task<GitResult> CreateBranchAsync(string name) => RunConnectedAsync(async repo =>
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name) || !Reference.IsValidName($"refs/heads/{name}") || name.StartsWith("-"))
        {
            return GitResult.Fail(GitErrorCodes.InvalidArgument, $"'{name}' is not a valid branch name");
        }

        if (IsMerging(repo))
        {
            return GitResult.Fail(GitErrorCodes.MergeInProgress, "A merge is in progress. Complete or abort the merge first.");
        }

        try
        {
            Fetch(repo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fetch before creating branch failed");
        }

        if (repo.Branches[name] != null || repo.Branches[$"{OriginName}/{name}"] != null)
        {
            return GitResult.Fail(GitErrorCodes.InvalidArgument, $"Branch '{name}' already exists");
        }

        var status = await BuildStatusAsync(repo, true);
        var onMain = repo.Head.FriendlyName == MainBranch;
        if (!onMain && status.Changes.Count > 0)
        {
            return WithStatus(GitResult.Fail(GitErrorCodes.UncommittedChanges,
                $"Branches are always created from '{MainBranch}'. Commit or discard your changes on '{repo.Head.FriendlyName}' first."), status);
        }

        var baseCommit = MainBaseCommit(repo);
        if (baseCommit == null)
        {
            return GitResult.Fail(GitErrorCodes.NotFound, $"Branch '{MainBranch}' not found");
        }

        var branch = repo.CreateBranch(name, baseCommit);
        Commands.Checkout(repo, branch);  // uncommitted changes on main are carried over to the new branch

        var import = await ImportAsync(repo);
        return WithStatus(WithImport(GitResult.Ok($"Branch '{name}' created from '{MainBranch}' and checked out"), import),
                          await BuildStatusAsync(repo, true));
    });

    public Task<GitResult> CheckoutAsync(string name) => RunConnectedAsync(async repo =>
    {
        if (IsMerging(repo))
        {
            return GitResult.Fail(GitErrorCodes.MergeInProgress, "A merge is in progress. Complete or abort the merge first.");
        }

        if (repo.Head.FriendlyName == name)
        {
            return WithStatus(GitResult.Ok($"'{name}' is already checked out"), await BuildStatusAsync(repo, true));
        }

        var status = await BuildStatusAsync(repo, true);
        if (status.Changes.Count > 0)
        {
            return WithStatus(GitResult.Fail(GitErrorCodes.UncommittedChanges,
                "You have uncommitted changes. Commit or discard them before switching the branch."), status);
        }

        var branch = LocalBranch(repo, name);
        if (branch == null)
        {
            return GitResult.Fail(GitErrorCodes.NotFound, $"Branch '{name}' not found");
        }

        Commands.Checkout(repo, branch);

        var import = await ImportAsync(repo);
        return WithStatus(WithImport(GitResult.Ok($"Switched to branch '{name}'"), import), await BuildStatusAsync(repo, true));
    });

    public Task<GitResult> DeleteBranchAsync(string name, bool deleteRemote) => RunConnectedAsync(async repo =>
    {
        if (string.IsNullOrWhiteSpace(name) || name == MainBranch)
        {
            return GitResult.Fail(GitErrorCodes.InvalidArgument, $"Branch '{MainBranch}' can not be deleted");
        }

        if (repo.Head.FriendlyName == name)
        {
            return GitResult.Fail(GitErrorCodes.InvalidArgument, "The current branch can not be deleted. Switch to another branch first.");
        }

        DeleteBranch(repo, name, deleteRemote);
        return WithStatus(GitResult.Ok($"Branch '{name}' deleted"), await BuildStatusAsync(repo, true));
    });

    public Task<GitResult> MergeIntoMainAsync(bool deleteBranch, string author) => RunConnectedAsync(async repo =>
    {
        if (IsMerging(repo))
        {
            return GitResult.Fail(GitErrorCodes.MergeInProgress, "A merge is in progress. Complete or abort the merge first.");
        }

        var sourceName = repo.Head.FriendlyName;
        if (sourceName == MainBranch)
        {
            return GitResult.Fail(GitErrorCodes.InvalidArgument, $"You are already on '{MainBranch}'");
        }

        var status = await BuildStatusAsync(repo, true);
        if (status.Changes.Count > 0)
        {
            return WithStatus(GitResult.Fail(GitErrorCodes.UncommittedChanges,
                "You have uncommitted changes. Commit or discard them before merging."), status);
        }

        Fetch(repo);
        var storage = await _mirror.SnapshotStorageAsync();

        // publish the branch so the merge is traceable on the remote
        var pushBranch = TryPush(repo);
        if (!pushBranch.Success)
        {
            return WithStatus(pushBranch, status);
        }

        var main = LocalBranch(repo, MainBranch);
        if (main == null)
        {
            return GitResult.Fail(GitErrorCodes.NotFound, $"Branch '{MainBranch}' not found");
        }

        Commands.Checkout(repo, main);
        var signature = Signature(author);

        var remoteMain = repo.Branches[$"{OriginName}/{MainBranch}"];
        if (remoteMain?.Tip != null)
        {
            var update = repo.Merge(remoteMain, signature, MergeOptions());
            if (update.Status == MergeStatus.Conflicts)
            {
                StartMerge(repo, new GitMergeInfo { Kind = "pull", Source = remoteMain.FriendlyName, Target = MainBranch, ReturnBranch = sourceName }, storage);
                return WithStatus(GitResult.Fail(GitErrorCodes.Conflicts,
                        $"Your local '{MainBranch}' has diverged from the remote. Resolve the conflicts, complete the merge and run 'Merge into {MainBranch}' again from '{sourceName}'."),
                    await BuildStatusAsync(repo, false));
            }
        }

        var source = repo.Branches[sourceName];
        var merge = repo.Merge(source, signature, MergeOptions());

        if (merge.Status == MergeStatus.Conflicts)
        {
            StartMerge(repo, new GitMergeInfo { Kind = "merge-into-main", Source = sourceName, Target = MainBranch, DeleteSourceBranch = deleteBranch, ReturnBranch = sourceName }, storage);
            return WithStatus(GitResult.Fail(GitErrorCodes.Conflicts,
                    $"Merging '{sourceName}' into '{MainBranch}' resulted in conflicts. Resolve them and complete the merge."),
                await BuildStatusAsync(repo, false));
        }

        var import = await ImportAsync(repo);
        var result = FinishMergeIntoMain(repo, sourceName, deleteBranch);
        return WithStatus(WithImport(result, import), await BuildStatusAsync(repo, true));
    });

    private GitResult FinishMergeIntoMain(Repository repo, string sourceName, bool deleteBranch)
    {
        var push = TryPush(repo);
        if (!push.Success)
        {
            return GitResult.Fail(push.ErrorCode, $"Merged '{sourceName}' into '{MainBranch}', but push failed: {push.Message}");
        }

        var message = $"Merged '{sourceName}' into '{MainBranch}' and pushed";

        if (deleteBranch)
        {
            try
            {
                DeleteBranch(repo, sourceName, true);
                message += $". Branch '{sourceName}' deleted";
            }
            catch (Exception ex)
            {
                message += $". Branch '{sourceName}' could not be deleted: {ToResult(ex).Message}";
            }
        }

        return GitResult.Ok(message);
    }

    #endregion

    #region Merge conflicts

    public Task<GitResult> ResolveAsync(string id, string side) => RunConnectedAsync(async repo =>
    {
        if (!IsMerging(repo))
        {
            return GitResult.Fail(GitErrorCodes.NoMergeInProgress, "No merge in progress");
        }

        if (side != "ours" && side != "theirs")
        {
            return GitResult.Fail(GitErrorCodes.InvalidArgument, "side must be 'ours' or 'theirs'");
        }

        var conflicts = repo.Index.Conflicts
            .Where(c => DataLinqGitPaths.ToItem(ConflictPath(c)).id == id)
            .ToList();

        if (conflicts.Count == 0)
        {
            return GitResult.Fail(GitErrorCodes.NotFound, $"No conflict for {id}");
        }

        foreach (var conflict in conflicts)
        {
            var path = ConflictPath(conflict);
            var entry = side == "ours" ? conflict.Ours : conflict.Theirs;
            var fullPath = DataLinqGitMirror.FullPath(repo.Info.WorkingDirectory, path);

            if (entry == null)
            {
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
                repo.Index.Remove(path);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                using (var stream = repo.Lookup<Blob>(entry.Id).GetContentStream())
                using (var file = File.Create(fullPath))
                {
                    stream.CopyTo(file);
                }
                repo.Index.Add(path);
            }
        }
        repo.Index.Write();

        return WithStatus(GitResult.Ok($"Conflict resolved for {id}"), await BuildStatusAsync(repo, false));
    });

    public Task<GitResult> CompleteMergeAsync(string message, string author) => RunConnectedAsync(async repo =>
    {
        if (!IsMerging(repo))
        {
            return GitResult.Fail(GitErrorCodes.NoMergeInProgress, "No merge in progress");
        }

        if (repo.Index.Conflicts.Any())
        {
            return WithStatus(GitResult.Fail(GitErrorCodes.UnresolvedConflicts, "There are unresolved conflicts"), await BuildStatusAsync(repo, false));
        }

        var info = ReadMergeInfo(repo);
        var signature = Signature(author);
        message = string.IsNullOrWhiteSpace(message)
            ? info?.Kind == "merge-into-main"
                ? $"Merge branch '{info.Source}' into {info.Target}"
                : $"Merge {info?.Source ?? "remote changes"}"
            : message.Trim();

        repo.Commit(message, signature, signature);

        // items saved in DataLinq while the merge was pending are kept (they show up as uncommitted changes)
        var editedDuringMerge = await EditedSinceMergeStartAsync(repo);
        DeleteMergeInfo(repo);

        var import = await ImportAsync(repo, editedDuringMerge);
        var result = info?.Kind == "merge-into-main"
            ? FinishMergeIntoMain(repo, info.Source, info.DeleteSourceBranch)
            : GitResult.Ok("Merge completed");

        if (editedDuringMerge.Count > 0)
        {
            result.Message += $". {editedDuringMerge.Count} item(s) changed in DataLinq during the merge are kept as uncommitted changes";
        }

        return WithStatus(WithImport(result, import), await BuildStatusAsync(repo, true));
    });

    public Task<GitResult> AbortMergeAsync() => RunConnectedAsync(async repo =>
    {
        if (!IsMerging(repo))
        {
            return GitResult.Fail(GitErrorCodes.NoMergeInProgress, "No merge in progress");
        }

        var info = ReadMergeInfo(repo);

        // nothing was imported during the merge: the DataLinq storage still holds the pre-merge state
        repo.Reset(ResetMode.Hard, repo.Head.Tip);
        DeleteMergeInfo(repo);

        // merge into main: the DataLinq storage still holds the content of the branch the merge was started from
        if (!string.IsNullOrEmpty(info?.ReturnBranch) && repo.Branches[info.ReturnBranch] != null && repo.Head.FriendlyName != info.ReturnBranch)
        {
            Commands.Checkout(repo, repo.Branches[info.ReturnBranch], new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });
        }

        return WithStatus(GitResult.Ok("Merge aborted"), await BuildStatusAsync(repo, true));
    });

    #endregion

    #region Discard / Diff / History

    public Task<GitResult> DiscardAsync(string[] ids) => RunConnectedAsync(async repo =>
    {
        if (IsMerging(repo))
        {
            return GitResult.Fail(GitErrorCodes.MergeInProgress, "A merge is in progress. Use 'Abort merge' instead.");
        }

        if (ids == null || ids.Length == 0)
        {
            return GitResult.Fail(GitErrorCodes.InvalidArgument, "Nothing selected");
        }

        var status = await BuildStatusAsync(repo, true);
        var paths = status.Changes.Where(c => ids.Contains(c.Id)).SelectMany(c => c.Paths).Distinct().ToList();
        var headTree = repo.Head.Tip?.Tree;

        foreach (var path in paths)
        {
            var fullPath = DataLinqGitMirror.FullPath(repo.Info.WorkingDirectory, path);
            var entry = headTree?[path];

            if (entry?.Target is Blob blob)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                using var stream = blob.GetContentStream();
                using var file = File.Create(fullPath);
                stream.CopyTo(file);
            }
            else if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                DataLinqGitMirror.DeleteEmptyDirectories(repo.Info.WorkingDirectory, Path.GetDirectoryName(fullPath));
            }
        }

        if (paths.Count > 0)
        {
            Commands.Unstage(repo, paths);
        }

        var import = await ImportAsync(repo);
        return WithStatus(WithImport(GitResult.Ok($"Discarded changes of {ids.Length} item(s)"), import), await BuildStatusAsync(repo, true));
    });

    /// <summary>
    /// Items that could not be imported: retry the import, or (keepDataLinq) let the DataLinq content win
    /// (the working copy files are overwritten and show up as changes)
    /// </summary>
    public Task<GitResult> ResolveImportErrorsAsync(string[] ids, bool keepDataLinq) => RunConnectedAsync(async repo =>
    {
        if (IsMerging(repo))
        {
            return GitResult.Fail(GitErrorCodes.MergeInProgress, "A merge is in progress. Complete or abort the merge first.");
        }

        if (keepDataLinq)
        {
            var errors = ReadImportErrors(repo);
            errors.ExceptWith(ids ?? errors.ToArray());
            WriteImportErrors(repo, errors);
            return WithStatus(GitResult.Ok("The DataLinq version is used"), await BuildStatusAsync(repo, true));
        }

        var import = await ImportAsync(repo);
        return WithStatus(WithImport(GitResult.Ok("Import retried"), import), await BuildStatusAsync(repo, true));
    });

    public Task<GitResult> DiffAsync(string id) => RunConnectedAsync(async repo =>
    {
        var merging = IsMerging(repo);
        if (!merging)
        {
            await ExportAsync(repo);
        }

        var diffs = new List<GitFileDiff>();
        var conflicts = merging
            ? repo.Index.Conflicts.Where(c => DataLinqGitPaths.ToItem(ConflictPath(c)).id == id).ToList()
            : new List<Conflict>();

        if (conflicts.Count > 0)
        {
            var info = ReadMergeInfo(repo);
            foreach (var conflict in conflicts)
            {
                var path = ConflictPath(conflict);
                diffs.Add(FileDiff(path, GitChangeStatus.Conflicted,
                    conflict.Ours == null ? null : BlobText(repo.Lookup<Blob>(conflict.Ours.Id)),
                    conflict.Theirs == null ? null : BlobText(repo.Lookup<Blob>(conflict.Theirs.Id)),
                    $"ours ({info?.Target ?? repo.Head.FriendlyName})",
                    $"theirs ({info?.Source ?? "incoming"})"));
            }
        }
        else
        {
            var headTree = repo.Head.Tip?.Tree;
            var paths = DataLinqGitPaths.ItemPaths(id);

            foreach (var path in paths)
            {
                var oldText = headTree?[path]?.Target is Blob blob ? BlobText(blob) : null;
                var fullPath = DataLinqGitMirror.FullPath(repo.Info.WorkingDirectory, path);
                var newText = File.Exists(fullPath) ? ReadText(fullPath) : null;

                if (oldText == null && newText == null || oldText == newText)
                {
                    continue;
                }

                diffs.Add(FileDiff(path,
                    oldText == null ? GitChangeStatus.Added : newText == null ? GitChangeStatus.Deleted : GitChangeStatus.Modified,
                    oldText, newText, $"HEAD ({repo.Head.FriendlyName})", "working copy"));
            }
        }

        return GitResult.Ok($"{diffs.Count} file(s)", diffs);
    });

    public Task<GitResult> HistoryAsync(int count, string id) => RunConnectedAsync(repo =>
    {
        count = Math.Clamp(count <= 0 ? 50 : count, 1, 500);

        if (repo.Head.Tip == null)
        {
            return Task.FromResult(GitResult.Ok("No commits", new List<GitCommitInfo>()));
        }

        var unpushed = new HashSet<string>();
        var remoteTips = repo.Branches.Where(b => b.IsRemote && b.Tip != null).Select(b => b.Tip).ToList();
        foreach (var c in repo.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = repo.Head.Tip, ExcludeReachableFrom = remoteTips }).Take(1000))
        {
            unpushed.Add(c.Sha);
        }

        var paths = string.IsNullOrEmpty(id) ? null : DataLinqGitPaths.ItemPaths(id);
        var commits = new List<GitCommitInfo>();
        var scanned = 0;

        foreach (var commit in repo.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = repo.Head.Tip, SortBy = CommitSortStrategies.Time | CommitSortStrategies.Topological }))
        {
            if (commits.Count >= count || ++scanned > 5000)
            {
                break;
            }

            if (paths != null && !TouchesPaths(commit, paths))
            {
                continue;
            }

            commits.Add(new GitCommitInfo
            {
                Sha = commit.Sha,
                ShortSha = commit.Sha.Substring(0, 7),
                Message = commit.Message,
                MessageShort = commit.MessageShort,
                Author = commit.Author.Name,
                Email = commit.Author.Email,
                Date = commit.Author.When,
                Parents = commit.Parents.Count(),
                IsPushed = !unpushed.Contains(commit.Sha)
            });
        }

        return Task.FromResult(GitResult.Ok($"{commits.Count} commits", commits));
    });

    public Task<GitResult> CommitDetailsAsync(string sha) => RunConnectedAsync(repo =>
    {
        var commit = string.IsNullOrWhiteSpace(sha) ? null : repo.Lookup<Commit>(sha);
        if (commit == null)
        {
            return Task.FromResult(GitResult.Fail(GitErrorCodes.NotFound, $"Commit {sha} not found"));
        }

        var parent = commit.Parents.FirstOrDefault();
        var changes = repo.Diff.Compare<TreeChanges>(parent?.Tree, commit.Tree);
        var diffs = new List<GitFileDiff>();

        foreach (var change in changes.Take(300))
        {
            var status = change.Status switch
            {
                ChangeKind.Added => GitChangeStatus.Added,
                ChangeKind.Deleted => GitChangeStatus.Deleted,
                _ => GitChangeStatus.Modified
            };

            diffs.Add(FileDiff(change.Path, status,
                change.Status == ChangeKind.Added ? null : BlobText(repo.Lookup<Blob>(change.OldOid)),
                change.Status == ChangeKind.Deleted ? null : BlobText(repo.Lookup<Blob>(change.Oid)),
                parent == null ? "(none)" : parent.Sha.Substring(0, 7),
                commit.Sha.Substring(0, 7)));
        }

        return Task.FromResult(GitResult.Ok(commit.Message, diffs));
    });

    #endregion

    #region Core helpers

    private async Task<GitResult> RunAsync(Func<Task<GitResult>> action)
    {
        if (!_options.UseVersionControl)
        {
            return GitResult.Fail(GitErrorCodes.NotConfigured, "Version control is not enabled");
        }

        if (!_options.IsConfigured)
        {
            return GitResult.Fail(GitErrorCodes.NotConfigured, "Version control is not configured (LocalRepositoryPath and RemoteUrl are required)");
        }

        await _lock.WaitAsync();
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Git operation failed");
            return ToResult(ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    private Task<GitResult> RunConnectedAsync(Func<Repository, Task<GitResult>> action) => RunAsync(async () =>
    {
        if (!Repository.IsValid(RepoPath))
        {
            return new GitResult
            {
                Success = false,
                ErrorCode = GitErrorCodes.NotConnected,
                Message = "Repository is not connected",
                Status = NotConnectedStatus()
            };
        }

        using var repo = OpenRepository();
        return await action(repo);
    });

    private Repository OpenRepository()
    {
        var repo = new Repository(RepoPath);

        var origin = repo.Network.Remotes[OriginName];
        if (origin == null)
        {
            repo.Network.Remotes.Add(OriginName, _options.RemoteUrl);
        }
        else if (origin.Url != _options.RemoteUrl)
        {
            repo.Network.Remotes.Update(OriginName, r => r.Url = _options.RemoteUrl);
        }

        return repo;
    }

    private GitStatusInfo NotConnectedStatus() => new GitStatusInfo
    {
        Enabled = true,
        Connected = false,
        RemoteUrl = MaskedRemoteUrl(),
        MainBranch = MainBranch
    };

    private async Task<GitStatusInfo> BuildStatusAsync(Repository repo, bool sync)
    {
        var merging = IsMerging(repo);
        if (sync && !merging)
        {
            await ExportAsync(repo);
        }

        var head = repo.Head;
        var status = new GitStatusInfo
        {
            Enabled = true,
            Connected = true,
            RemoteUrl = MaskedRemoteUrl(),
            Branch = head.FriendlyName,
            MainBranch = MainBranch,
            IsMainBranch = head.FriendlyName == MainBranch,
            HasUpstream = head.TrackedBranch?.Tip != null,
            Ahead = head.TrackedBranch?.Tip != null ? head.TrackingDetails?.AheadBy ?? 0 : UnpublishedCommits(repo),
            Behind = head.TrackingDetails?.BehindBy ?? 0,
            HeadSha = head.Tip?.Sha,
            HeadMessage = head.Tip?.MessageShort,
            Merging = merging,
            MergeInfo = merging ? ReadMergeInfo(repo) ?? new GitMergeInfo { Kind = "pull" } : null
        };

        var importErrors = ReadImportErrors(repo);
        if (importErrors.Count > 0)
        {
            status.ImportErrors = importErrors.OrderBy(i => i).ToList();
        }

        var entries = repo.RetrieveStatus(new StatusOptions
        {
            IncludeUntracked = true,
            RecurseUntrackedDirs = true,
            IncludeIgnored = false,
            DetectRenamesInIndex = false,
            DetectRenamesInWorkDir = false
        });

        var groups = new Dictionary<string, List<(string path, string status, bool staged)>>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var fileStatus = FileStatusOf(entry.State);
            if (fileStatus == null)
            {
                continue;
            }

            var path = DataLinqGitPaths.Normalize(entry.FilePath);
            var id = DataLinqGitPaths.ToItem(path).id;
            if (!groups.TryGetValue(id, out var list))
            {
                groups[id] = list = new();
            }

            var staged = (entry.State & (FileStatus.NewInIndex | FileStatus.ModifiedInIndex | FileStatus.DeletedFromIndex | FileStatus.RenamedInIndex | FileStatus.TypeChangeInIndex)) != 0;
            list.Add((path, fileStatus, staged));
        }

        foreach (var group in groups.OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var statuses = group.Value.Select(v => v.status).ToList();
            status.Changes.Add(new GitChange
            {
                Id = group.Key,
                Kind = DataLinqGitPaths.ToItem(group.Value[0].path).kind,
                Paths = group.Value.Select(v => v.path).OrderBy(p => p).ToList(),
                Status = statuses.Contains(GitChangeStatus.Conflicted) ? GitChangeStatus.Conflicted
                       : statuses.All(s => s == GitChangeStatus.Added) ? GitChangeStatus.Added
                       : statuses.All(s => s == GitChangeStatus.Deleted) ? GitChangeStatus.Deleted
                       : GitChangeStatus.Modified,
                Staged = merging ? group.Value.All(v => v.staged) : null
            });
        }

        return status;
    }

    private static string FileStatusOf(FileStatus state)
    {
        if (state.HasFlag(FileStatus.Conflicted))
        {
            return GitChangeStatus.Conflicted;
        }

        if (state == FileStatus.Unaltered || state.HasFlag(FileStatus.Ignored))
        {
            return null;
        }

        if (state.HasFlag(FileStatus.NewInWorkdir) || state.HasFlag(FileStatus.NewInIndex))
        {
            return state.HasFlag(FileStatus.DeletedFromWorkdir) ? null : GitChangeStatus.Added;
        }

        if (state.HasFlag(FileStatus.DeletedFromWorkdir) || state.HasFlag(FileStatus.DeletedFromIndex))
        {
            return GitChangeStatus.Deleted;
        }

        return GitChangeStatus.Modified;
    }

    /// <summary>
    /// DataLinq storage => working tree (storage wins)
    /// </summary>
    private async Task<Dictionary<string, string>> ExportAsync(Repository repo)
    {
        var storage = await _mirror.SnapshotStorageAsync();
        var tree = DataLinqGitMirror.ReadWorkingTree(repo.Info.WorkingDirectory);

        // items that could not be imported must not be overwritten/deleted with the (outdated) DataLinq content
        var importErrors = ReadImportErrors(repo);
        var items = DataLinqGitMirror.DiffItems(storage, tree).Where(id => !importErrors.Contains(id)).ToList();

        if (items.Count > 0)
        {
            DataLinqGitMirror.Export(repo.Info.WorkingDirectory, storage, items);
        }

        return storage;
    }

    /// <summary>
    /// working tree => DataLinq storage (working tree wins)
    /// </summary>
    private async Task<DataLinqGitMirror.ImportResult> ImportAsync(Repository repo, ICollection<string> skip = null)
    {
        var storage = await _mirror.SnapshotStorageAsync();
        var tree = DataLinqGitMirror.ReadWorkingTree(repo.Info.WorkingDirectory);
        var items = DataLinqGitMirror.DiffItems(storage, tree).Where(id => skip == null || !skip.Contains(id)).ToList();

        if (items.Count == 0)
        {
            WriteImportErrors(repo, Array.Empty<string>());
            return new DataLinqGitMirror.ImportResult();
        }

        var result = await _mirror.ImportAsync(tree, items);

        // verify: items that still differ and were not imported could not be loaded (e.g. invalid ids).
        // Imported items may still differ in formatting only (hand edited json) => shown as modified by the next status.
        var remaining = DataLinqGitMirror.DiffItems(await _mirror.SnapshotStorageAsync(), tree);
        foreach (var id in remaining.Where(id =>
                     DataLinqGitPaths.KindOf(id) != GitItemKinds.File &&
                     (skip == null || !skip.Contains(id)) &&
                     !result.Updated.Contains(id) &&
                     !result.Deleted.Contains(id) &&
                     !result.Errors.Any(e => e.StartsWith(id + ":"))))
        {
            result.Errors.Add($"{id}: could not be imported into DataLinq");
            result.FailedIds.Add(id);
        }

        WriteImportErrors(repo, result.FailedIds);
        return result;
    }

    private void Fetch(Repository repo)
    {
        var remote = repo.Network.Remotes[OriginName];
        var refSpecs = remote.FetchRefSpecs.Select(r => r.Specification).ToList();
        if (refSpecs.Count == 0)
        {
            refSpecs.Add($"+refs/heads/*:refs/remotes/{OriginName}/*");
        }

        Commands.Fetch(repo, OriginName, refSpecs, new FetchOptions
        {
            CredentialsProvider = CredentialsHandler(),
            Prune = true
        }, null);
    }

    private GitResult TryPush(Repository repo)
    {
        try
        {
            return Push(repo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Push failed");
            return ToResult(ex);
        }
    }

    private GitResult Push(Repository repo)
    {
        var head = repo.Head;
        if (head.Tip == null)
        {
            return GitResult.Fail(GitErrorCodes.NothingToCommit, "There are no commits to push");
        }

        string pushError = null;
        var options = new PushOptions
        {
            CredentialsProvider = CredentialsHandler(),
            OnPushStatusError = error => pushError = $"{error.Reference}: {error.Message}"
        };

        repo.Network.Push(repo.Network.Remotes[OriginName], $"{head.CanonicalName}:{head.CanonicalName}", options);

        if (pushError != null)
        {
            return GitResult.Fail(pushError.Contains("fast-forward") || pushError.Contains("fetch first")
                    ? GitErrorCodes.NonFastForward
                    : GitErrorCodes.Error,
                $"Push rejected ({pushError}). Pull first.");
        }

        if (head.TrackedBranch == null || head.TrackedBranch.Tip == null)
        {
            repo.Branches.Update(head,
                b => b.Remote = OriginName,
                b => b.UpstreamBranch = head.CanonicalName);
        }

        return GitResult.Ok("pushed", $"{OriginName}/{head.FriendlyName}");
    }

    private Branch EnsureUpstream(Repository repo, Branch head)
    {
        if (head.TrackedBranch?.Tip != null)
        {
            return head.TrackedBranch;
        }

        var remoteBranch = repo.Branches[$"{OriginName}/{head.FriendlyName}"];
        if (remoteBranch == null)
        {
            return null;
        }

        repo.Branches.Update(head,
            b => b.Remote = OriginName,
            b => b.UpstreamBranch = head.CanonicalName);

        return repo.Branches[head.FriendlyName].TrackedBranch;
    }

    private Branch LocalBranch(Repository repo, string name)
    {
        var local = repo.Branches[name];
        if (local != null && !local.IsRemote)
        {
            return local;
        }

        var remote = repo.Branches[$"{OriginName}/{name}"];
        if (remote == null)
        {
            return null;
        }

        local = repo.CreateBranch(name, remote.Tip);
        repo.Branches.Update(local,
            b => b.Remote = OriginName,
            b => b.UpstreamBranch = local.CanonicalName);

        return repo.Branches[name];
    }

    /// <summary>
    /// New branches are based on the newer of local and remote main
    /// </summary>
    private Commit MainBaseCommit(Repository repo)
    {
        var local = repo.Branches[MainBranch]?.Tip;
        var remote = repo.Branches[$"{OriginName}/{MainBranch}"]?.Tip;

        if (local == null)
        {
            return remote;
        }

        if (remote == null)
        {
            return local;
        }

        var divergence = repo.ObjectDatabase.CalculateHistoryDivergence(local, remote);
        return divergence.AheadBy == 0 && divergence.BehindBy > 0 ? remote : local;
    }

    private void DeleteBranch(Repository repo, string name, bool deleteRemote)
    {
        var local = repo.Branches[name];
        if (local != null && !local.IsRemote)
        {
            repo.Branches.Remove(local);
        }

        if (deleteRemote && repo.Branches[$"{OriginName}/{name}"] != null)
        {
            string pushError = null;
            repo.Network.Push(repo.Network.Remotes[OriginName], $":refs/heads/{name}", new PushOptions
            {
                CredentialsProvider = CredentialsHandler(),
                OnPushStatusError = error => pushError = error.Message
            });

            if (pushError != null)
            {
                throw new Exception(pushError);
            }

            var remoteBranch = repo.Branches[$"{OriginName}/{name}"];
            if (remoteBranch != null)
            {
                repo.Branches.Remove(remoteBranch);
            }
        }
    }

    private static MergeOptions MergeOptions() => new MergeOptions
    {
        FailOnConflict = false,
        CommitOnSuccess = true,
        FastForwardStrategy = FastForwardStrategy.Default,
        FileConflictStrategy = CheckoutFileConflictStrategy.Merge
    };

    private static int UnpublishedCommits(Repository repo)
    {
        if (repo.Head.Tip == null)
        {
            return 0;
        }

        var remoteTips = repo.Branches.Where(b => b.IsRemote && b.Tip != null).Select(b => b.Tip).ToList();
        return repo.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = repo.Head.Tip, ExcludeReachableFrom = remoteTips }).Take(1000).Count();
    }

    private static bool IsMerging(Repository repo)
        => repo.Info.CurrentOperation == CurrentOperation.Merge || repo.Index.Conflicts.Any();

    private static string ConflictPath(Conflict conflict)
        => DataLinqGitPaths.Normalize((conflict.Ours ?? conflict.Theirs ?? conflict.Ancestor).Path);

    private void StartMerge(Repository repo, GitMergeInfo info, Dictionary<string, string> storage)
    {
        File.WriteAllText(Path.Combine(repo.Info.Path, MergeInfoFile), JsonConvert.SerializeObject(info));
        File.WriteAllText(Path.Combine(repo.Info.Path, MergeSnapshotFile),
            JsonConvert.SerializeObject(storage.ToDictionary(f => f.Key, f => Hash(f.Value))));
    }

    /// <summary>
    /// Items that were saved in DataLinq after the merge started
    /// </summary>
    private async Task<HashSet<string>> EditedSinceMergeStartAsync(Repository repo)
    {
        var edited = new HashSet<string>(StringComparer.Ordinal);
        var path = Path.Combine(repo.Info.Path, MergeSnapshotFile);
        if (!File.Exists(path))
        {
            return edited;
        }

        var snapshot = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path)) ?? new();
        var current = (await _mirror.SnapshotStorageAsync()).ToDictionary(f => f.Key, f => Hash(f.Value));

        foreach (var file in snapshot.Keys.Union(current.Keys))
        {
            snapshot.TryGetValue(file, out var before);
            current.TryGetValue(file, out var now);
            if (before != now)
            {
                edited.Add(DataLinqGitPaths.ToItem(file).id);
            }
        }

        return edited;
    }

    private static string Hash(string content)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(content ?? string.Empty)));

    private static HashSet<string> ReadImportErrors(Repository repo)
    {
        var path = Path.Combine(repo.Info.Path, ImportErrorsFile);
        try
        {
            return File.Exists(path)
                ? new HashSet<string>(JsonConvert.DeserializeObject<string[]>(File.ReadAllText(path)) ?? [], StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
        }
        catch
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private static void WriteImportErrors(Repository repo, IEnumerable<string> ids)
    {
        var path = Path.Combine(repo.Info.Path, ImportErrorsFile);
        var list = ids?.Distinct().ToArray() ?? [];

        if (list.Length == 0)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            return;
        }

        File.WriteAllText(path, JsonConvert.SerializeObject(list));
    }

    private static GitMergeInfo ReadMergeInfo(Repository repo)
    {
        var path = Path.Combine(repo.Info.Path, MergeInfoFile);
        try
        {
            return File.Exists(path) ? JsonConvert.DeserializeObject<GitMergeInfo>(File.ReadAllText(path)) : null;
        }
        catch
        {
            return null;
        }
    }

    private static void DeleteMergeInfo(Repository repo)
    {
        foreach (var file in new[] { MergeInfoFile, MergeSnapshotFile })
        {
            var path = Path.Combine(repo.Info.Path, file);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static bool TouchesPaths(Commit commit, string[] paths)
    {
        var parents = commit.Parents.ToList();
        foreach (var path in paths)
        {
            var current = commit.Tree[path]?.Target?.Sha;
            if (parents.Count == 0)
            {
                if (current != null)
                {
                    return true;
                }
                continue;
            }

            if (parents.All(p => p.Tree[path]?.Target?.Sha != current))
            {
                return true;
            }
        }

        return false;
    }

    private Signature Signature(string author)
        => new Signature(string.IsNullOrWhiteSpace(author) ? "DataLinq" : author,
                         string.IsNullOrWhiteSpace(_options.AuthorEmail) ? "datalinq@localhost" : _options.AuthorEmail,
                         DateTimeOffset.Now);

    private CredentialsHandler CredentialsHandler()
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            return null;
        }

        return (url, usernameFromUrl, types) => new UsernamePasswordCredentials
        {
            Username = string.IsNullOrWhiteSpace(_options.AccessTokenUserName) ? "datalinq" : _options.AccessTokenUserName,
            Password = _options.AccessToken
        };
    }

    private string MaskedRemoteUrl()
    {
        try
        {
            var uri = new Uri(_options.RemoteUrl);
            return string.IsNullOrEmpty(uri.UserInfo) ? _options.RemoteUrl : _options.RemoteUrl.Replace(uri.UserInfo + "@", "***@");
        }
        catch
        {
            return _options.RemoteUrl;
        }
    }

    private GitResult CheckLocalPath()
    {
        var repoPath = RepoPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var storage = _persistanceOptions?.ConnectionString;

        if (!string.IsNullOrWhiteSpace(storage))
        {
            var storagePath = Path.GetFullPath(storage).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (repoPath.StartsWith(storagePath, StringComparison.OrdinalIgnoreCase) ||
                storagePath.StartsWith(repoPath, StringComparison.OrdinalIgnoreCase))
            {
                return GitResult.Fail(GitErrorCodes.LocalPathNotEmpty,
                    "VersionControl:LocalRepositoryPath must not be the DataLinq storage path (or a sub/parent folder of it)");
            }
        }

        if (Directory.Exists(RepoPath) && Directory.EnumerateFileSystemEntries(RepoPath).Any())
        {
            return GitResult.Fail(GitErrorCodes.LocalPathNotEmpty,
                $"The local repository folder '{RepoPath}' is not empty. Use an empty folder for VersionControl:LocalRepositoryPath.");
        }

        return null;
    }

    private void WriteRepositoryFiles(Repository repo)
    {
        var root = repo.Info.WorkingDirectory;

        var attributes = Path.Combine(root, ".gitattributes");
        if (!File.Exists(attributes))
        {
            // keep the DataLinq content byte by byte (no line ending conversion)
            File.WriteAllText(attributes, "* -text\n", Utf8NoBom);
        }

        var readme = Path.Combine(root, "README.md");
        if (!File.Exists(readme))
        {
            File.WriteAllText(readme,
                "# DataLinq\n\n" +
                "This repository is managed by DataLinq Code (version control).\n\n" +
                "```\n" +
                "_folders.json                          tree folders\n" +
                "_errorpages/{name}.razor               error pages\n" +
                "{endpoint}/endpoint.json|.css|.js      endpoint, global css/js\n" +
                "{endpoint}/queries/{query}/query.json  query settings\n" +
                "{endpoint}/queries/{query}/query.sql   query statement\n" +
                "{endpoint}/queries/{query}/views/{view}/view.json|.razor|.css|.js\n" +
                "```\n\n" +
                "Connection strings are stored as they are configured in DataLinq. Use secrets/constants stores for credentials.\n",
                Utf8NoBom);
        }
    }

    private static GitFileDiff FileDiff(string path, string status, string oldText, string newText, string oldLabel, string newLabel)
    {
        var item = DataLinqGitPaths.ToItem(path);
        var isBinary = (oldText?.Contains('\0') ?? false) || (newText?.Contains('\0') ?? false);

        return new GitFileDiff
        {
            Path = path,
            Id = item.id,
            Kind = item.kind,
            Status = status,
            OldLabel = oldLabel,
            NewLabel = newLabel,
            IsBinary = isBinary,
            OldText = isBinary ? null : Truncate(oldText),
            NewText = isBinary ? null : Truncate(newText)
        };
    }

    private static string Truncate(string text)
        => text != null && text.Length > MaxDiffTextLength ? text.Substring(0, MaxDiffTextLength) + "\n... (truncated)" : text;

    private static string BlobText(Blob blob)
    {
        if (blob == null)
        {
            return null;
        }

        using var reader = new StreamReader(blob.GetContentStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string ReadText(string path) => File.ReadAllText(path, Encoding.UTF8);

    private static GitResult WithStatus(GitResult result, GitStatusInfo status)
    {
        result.Status = status;
        return result;
    }

    private static GitResult WithImport(GitResult result, DataLinqGitMirror.ImportResult import)
    {
        if (import.Updated.Count > 0)
        {
            result.Imported = import.Updated;
        }
        if (import.Deleted.Count > 0)
        {
            result.Deleted = import.Deleted;
        }
        if (import.Errors.Count > 0)
        {
            result.ImportErrors = import.Errors;
            result.Message += $" ({import.Errors.Count} item(s) could not be imported into DataLinq)";
        }

        return result;
    }

    private static void DeleteDirectory(string path)
    {
        var di = new DirectoryInfo(path);
        if (!di.Exists)
        {
            return;
        }

        foreach (var fi in di.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            fi.Attributes = FileAttributes.Normal;  // git objects are read-only
        }

        di.Delete(true);
    }

    private static GitResult ToResult(Exception ex)
    {
        var message = ex.Message ?? string.Empty;
        var lower = message.ToLowerInvariant();

        return ex switch
        {
            CheckoutConflictException => GitResult.Fail(GitErrorCodes.LocalChangesConflict,
                "Your uncommitted changes would be overwritten by this operation. Commit or discard them first."),
            NonFastForwardException => GitResult.Fail(GitErrorCodes.NonFastForward,
                "The remote contains commits you do not have locally. Pull first."),
            EmptyCommitException => GitResult.Fail(GitErrorCodes.NothingToCommit, "There is nothing to commit"),
            RepositoryNotFoundException => GitResult.Fail(GitErrorCodes.NotConnected, "Repository not found"),
            _ when lower.Contains("401") || lower.Contains("403") || lower.Contains("authentication") || lower.Contains("credentials") || lower.Contains("unauthorized")
                => GitResult.Fail(GitErrorCodes.AuthFailed, $"Authentication failed. Check VersionControl:AccessToken ({message})"),
            _ when lower.Contains("failed to send request") || lower.Contains("failed to connect") || lower.Contains("could not resolve") ||
                   lower.Contains("timed out") || lower.Contains("timeout") || lower.Contains("connection") || lower.Contains("404")
                => GitResult.Fail(GitErrorCodes.Network, $"Remote repository not reachable ({message})"),
            _ => GitResult.Fail(GitErrorCodes.Error, message)
        };
    }

    #endregion
}
