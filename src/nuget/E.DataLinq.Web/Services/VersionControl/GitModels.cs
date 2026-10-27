using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace E.DataLinq.Web.Services.VersionControl;

public static class GitErrorCodes
{
    public const string NotConfigured = "not-configured";
    public const string NotConnected = "not-connected";
    public const string NotAuthorized = "not-authorized";
    public const string RemoteNotEmpty = "remote-not-empty";
    public const string LocalPathNotEmpty = "local-path-not-empty";
    public const string AuthFailed = "auth-failed";
    public const string Network = "network";
    public const string NothingToCommit = "nothing-to-commit";
    public const string UncommittedChanges = "uncommitted-changes";
    public const string LocalChangesConflict = "local-changes-conflict";
    public const string NonFastForward = "non-fast-forward";
    public const string Conflicts = "conflicts";
    public const string MergeInProgress = "merge-in-progress";
    public const string NoMergeInProgress = "no-merge-in-progress";
    public const string UnresolvedConflicts = "unresolved-conflicts";
    public const string InvalidArgument = "invalid-argument";
    public const string NotFound = "not-found";
    public const string Error = "error";
}

public static class GitChangeStatus
{
    public const string Added = "added";
    public const string Modified = "modified";
    public const string Deleted = "deleted";
    public const string Conflicted = "conflicted";
}

public static class GitItemKinds
{
    public const string Endpoint = "endpoint";
    public const string EndpointCss = "endpoint-css";
    public const string EndpointJs = "endpoint-js";
    public const string Query = "query";
    public const string View = "view";
    public const string ViewCss = "view-css";
    public const string ViewJs = "view-js";
    public const string ErrorPage = "errorpage";
    public const string Folders = "folders";
    public const string File = "file";
}

public class GitResult
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }

    [JsonProperty("errorCode", NullValueHandling = NullValueHandling.Ignore)]
    public string ErrorCode { get; set; }

    [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
    public object Data { get; set; }

    /// <summary>DataLinq ids that were written into (or deleted from) the DataLinq storage by this operation</summary>
    [JsonProperty("imported", NullValueHandling = NullValueHandling.Ignore)]
    public List<string> Imported { get; set; }

    [JsonProperty("deleted", NullValueHandling = NullValueHandling.Ignore)]
    public List<string> Deleted { get; set; }

    [JsonProperty("importErrors", NullValueHandling = NullValueHandling.Ignore)]
    public List<string> ImportErrors { get; set; }

    [JsonProperty("status", NullValueHandling = NullValueHandling.Ignore)]
    public GitStatusInfo Status { get; set; }

    public static GitResult Ok(string message, object data = null)
        => new GitResult { Success = true, Message = message, Data = data };

    public static GitResult Fail(string errorCode, string message, object data = null)
        => new GitResult { Success = false, ErrorCode = errorCode, Message = message, Data = data };
}

public class GitStatusInfo
{
    [JsonProperty("enabled")]
    public bool Enabled { get; set; }

    [JsonProperty("connected")]
    public bool Connected { get; set; }

    [JsonProperty("remoteUrl")]
    public string RemoteUrl { get; set; }

    [JsonProperty("branch")]
    public string Branch { get; set; }

    [JsonProperty("mainBranch")]
    public string MainBranch { get; set; }

    [JsonProperty("isMainBranch")]
    public bool IsMainBranch { get; set; }

    [JsonProperty("hasUpstream")]
    public bool HasUpstream { get; set; }

    [JsonProperty("ahead")]
    public int Ahead { get; set; }

    [JsonProperty("behind")]
    public int Behind { get; set; }

    [JsonProperty("headSha")]
    public string HeadSha { get; set; }

    [JsonProperty("headMessage")]
    public string HeadMessage { get; set; }

    [JsonProperty("merging")]
    public bool Merging { get; set; }

    [JsonProperty("mergeInfo", NullValueHandling = NullValueHandling.Ignore)]
    public GitMergeInfo MergeInfo { get; set; }

    [JsonProperty("changes")]
    public List<GitChange> Changes { get; set; } = new();

    /// <summary>items of the working copy that could not be imported into DataLinq (they are not overwritten by DataLinq content)</summary>
    [JsonProperty("importErrors", NullValueHandling = NullValueHandling.Ignore)]
    public List<string> ImportErrors { get; set; }
}

public class GitChange
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("kind")]
    public string Kind { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("paths")]
    public List<string> Paths { get; set; } = new();

    [JsonProperty("staged", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Staged { get; set; }
}

public class GitMergeInfo
{
    /// <summary>"pull" or "merge-into-main"</summary>
    [JsonProperty("kind")]
    public string Kind { get; set; }

    [JsonProperty("source")]
    public string Source { get; set; }

    [JsonProperty("target")]
    public string Target { get; set; }

    [JsonProperty("deleteSourceBranch")]
    public bool DeleteSourceBranch { get; set; }

    /// <summary>branch that is checked out again if the merge is aborted</summary>
    [JsonProperty("returnBranch", NullValueHandling = NullValueHandling.Ignore)]
    public string ReturnBranch { get; set; }
}

public class GitBranchInfo
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("isCurrent")]
    public bool IsCurrent { get; set; }

    [JsonProperty("isMain")]
    public bool IsMain { get; set; }

    [JsonProperty("isLocal")]
    public bool IsLocal { get; set; }

    [JsonProperty("isRemote")]
    public bool IsRemote { get; set; }

    [JsonProperty("ahead")]
    public int Ahead { get; set; }

    [JsonProperty("behind")]
    public int Behind { get; set; }

    [JsonProperty("lastCommit")]
    public DateTimeOffset? LastCommit { get; set; }
}

public class GitCommitInfo
{
    [JsonProperty("sha")]
    public string Sha { get; set; }

    [JsonProperty("shortSha")]
    public string ShortSha { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }

    [JsonProperty("messageShort")]
    public string MessageShort { get; set; }

    [JsonProperty("author")]
    public string Author { get; set; }

    [JsonProperty("email")]
    public string Email { get; set; }

    [JsonProperty("date")]
    public DateTimeOffset Date { get; set; }

    [JsonProperty("parents")]
    public int Parents { get; set; }

    [JsonProperty("isPushed")]
    public bool IsPushed { get; set; }
}

public class GitFileDiff
{
    [JsonProperty("path")]
    public string Path { get; set; }

    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("kind")]
    public string Kind { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("oldLabel")]
    public string OldLabel { get; set; }

    [JsonProperty("newLabel")]
    public string NewLabel { get; set; }

    [JsonProperty("oldText")]
    public string OldText { get; set; }

    [JsonProperty("newText")]
    public string NewText { get; set; }

    [JsonProperty("isBinary")]
    public bool IsBinary { get; set; }
}

public class GitCommitRequest
{
    [JsonProperty("message")]
    public string Message { get; set; }

    [JsonProperty("ids")]
    public string[] Ids { get; set; }

    [JsonProperty("push")]
    public bool Push { get; set; }
}

public class GitRequest
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("ids")]
    public string[] Ids { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("mode")]
    public string Mode { get; set; }

    [JsonProperty("side")]
    public string Side { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }

    [JsonProperty("sha")]
    public string Sha { get; set; }

    [JsonProperty("count")]
    public int? Count { get; set; }

    [JsonProperty("fetch")]
    public bool Fetch { get; set; }

    [JsonProperty("deleteBranch")]
    public bool DeleteBranch { get; set; }

    [JsonProperty("push")]
    public bool Push { get; set; }
}
