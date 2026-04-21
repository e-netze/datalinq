using E.DataLinq.Core.Models;
using E.DataLinq.Core.Models.Abstraction;
using E.DataLinq.Web.Services.Abstraction;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using static E.DataLinq.Web.Services.DataLinqVersionControlOptions;

namespace E.DataLinq.Web.Services;

public class GitService : IGitService, IDisposable
{
    private readonly DataLinqVersionControlOptions _options;
    private readonly ILogger<GitService> _logger;
    private Repository _repository;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public GitService(
        IOptions<DataLinqVersionControlOptions> options,
        ILogger<GitService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled => _options.UseVersionControl;

    public async Task<GitOperationResult> CloneAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (Repository.IsValid(_options.LocalRepositoryPath))
                    {
                        _logger.LogWarning("Repository already exists at {Path}", _options.LocalRepositoryPath);
                        return GitOperationResult.Ok("Repository already exists");
                    }

                    var cloneOptions = new CloneOptions
                    {
                        BranchName = _options.DefaultBranch,
                        Checkout = true,
                        OnCheckoutProgress = (path, completedSteps, totalSteps) =>
                        {
                            _logger.LogDebug("Checkout progress: {Path} {Completed}/{Total}",
                                path, completedSteps, totalSteps);
                        },
                        FetchOptions =
                        {
                            CredentialsProvider = GetCredentialsHandler()
                        }
                    };

                    var path = Repository.Clone(
                        _options.RemoteUrl,
                        _options.LocalRepositoryPath,
                        cloneOptions);

                    _logger.LogInformation("Repository cloned successfully to {Path}", path);
                    return GitOperationResult.Ok("Repository cloned successfully", new { Path = path });
                }
                catch (LibGit2SharpException ex) when (ex.Message.Contains("authentication"))
                {
                    _logger.LogError(ex, "Authentication failed during clone");
                    return GitOperationResult.Fail("Authentication failed", new GitOperationError
                    {
                        Type = GitErrorType.AuthenticationFailed,
                        Details = "Invalid credentials or access token",
                        Exception = ex
                    });
                }
                catch (LibGit2SharpException ex) when (ex.Message.Contains("network") || ex.Message.Contains("timeout"))
                {
                    _logger.LogError(ex, "Network error during clone");
                    return GitOperationResult.Fail("Network error", new GitOperationError
                    {
                        Type = GitErrorType.NetworkError,
                        Details = "Unable to reach remote repository",
                        Exception = ex
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error during clone");
                    return GitOperationResult.Fail("Clone failed", new GitOperationError
                    {
                        Type = GitErrorType.Unknown,
                        Details = ex.Message,
                        Exception = ex
                    });
                }
            }, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<GitOperationResult> CommitAndPushAsync(string message, string userName = "", CancellationToken cancellationToken = default)
    {
        var commitResult = await CommitAsync(message, userName, cancellationToken);
        if (!commitResult.Success)
        {
            if (commitResult.Error?.Type == GitErrorType.NothingToCommit)
                return GitOperationResult.Ok("No changes to push");

            return commitResult;
        }

        var pushResult = await PushAsync(cancellationToken);
        return pushResult;
    }

    public async Task<GitOperationResult> CommitAsync(string message, string userName = "", CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return GitOperationResult.Fail("Commit message required", new GitOperationError
                    {
                        Type = GitErrorType.InvalidOperation,
                        Details = "Commit message cannot be empty"
                    });
                }

                EnsureRepository();

                var status = _repository.RetrieveStatus();
                if (!status.IsDirty)
                {
                    _logger.LogInformation("No changes to commit");
                    return GitOperationResult.Fail("No changes to commit", new GitOperationError
                    {
                        Type = GitErrorType.NothingToCommit,
                        Details = "No changes to commit"
                    });
                }

                Commands.Stage(_repository, "*");

                var signature = new Signature(
                    string.IsNullOrEmpty(userName) ? _options.DefaultAuthorName : userName,
                    _options.DefaultAuthorEmail,
                    DateTimeOffset.Now);

                var commit = _repository.Commit(message, signature, signature);

                _logger.LogInformation("Changes committed: {Sha} - {Message}", commit.Sha, message);
                return GitOperationResult.Ok("Changes committed successfully", new
                {
                    CommitSha = commit.Sha,
                    Message = message,
                    FilesChanged = status.Count()
                });
            }
            catch (EmptyCommitException ex)
            {
                _logger.LogWarning("Nothing to commit");
                return GitOperationResult.Fail("Nothing to commit", new GitOperationError
                {
                    Type = GitErrorType.NothingToCommit,
                    Details = "Working directory clean",
                    Exception = ex
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during commit");
                return GitOperationResult.Fail("Commit failed", new GitOperationError
                {
                    Type = GitErrorType.Unknown,
                    Details = ex.Message,
                    Exception = ex
                });
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<GitOperationResult> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<GitOperationResult> PullAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<GitOperationResult> PushAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() =>
            {
                try
                {
                    EnsureRepository();

                    var pushOptions = new PushOptions
                    {
                        CredentialsProvider = GetCredentialsHandler(),
                        OnPushStatusError = pushStatusErrors =>
                        {
                            _logger.LogError("Push error: {Error}", pushStatusErrors.Message);
                        },
                        OnPackBuilderProgress = (stage, current, total) =>
                        {
                            _logger.LogDebug("Pack building: {Stage} {Current}/{Total}", stage, current, total);
                            return !cancellationToken.IsCancellationRequested;
                        }
                    };

                    var remote = _repository.Network.Remotes["origin"];
                    if (remote == null)
                    {
                        return GitOperationResult.Fail("Remote not found", new GitOperationError
                        {
                            Type = GitErrorType.InvalidOperation,
                            Details = "Origin remote is not configured"
                        });
                    }

                    var branch = _repository.Head;
                    if (branch.Tip == null)
                    {
                        return GitOperationResult.Fail("No commits to push", new GitOperationError
                        {
                            Type = GitErrorType.NothingToCommit,
                            Details = "Branch has no commits"
                        });
                    }

                    var pushRefSpec = $"{branch.CanonicalName}:{branch.CanonicalName}";
                    _repository.Network.Push(remote, pushRefSpec, pushOptions);

                    _logger.LogInformation("Push completed successfully to {Remote}", remote.Name);
                    return GitOperationResult.Ok("Push completed successfully", new
                    {
                        Remote = remote.Name,
                        Branch = branch.FriendlyName
                    });
                }
                catch (LibGit2SharpException ex) when (ex.Message.Contains("authentication"))
                {
                    _logger.LogError(ex, "Authentication failed during push");
                    return GitOperationResult.Fail("Authentication failed", new GitOperationError
                    {
                        Type = GitErrorType.AuthenticationFailed,
                        Details = "Invalid credentials or insufficient permissions",
                        Exception = ex
                    });
                }
                catch (LibGit2SharpException ex) when (ex.Message.Contains("rejected") || ex.Message.Contains("non-fast-forward"))
                {
                    _logger.LogError(ex, "Push rejected by remote");
                    return GitOperationResult.Fail("Push rejected", new GitOperationError
                    {
                        Type = GitErrorType.RemoteRejected,
                        Details = "Remote contains work you do not have locally. Pull first.",
                        Exception = ex
                    });
                }
                catch (LibGit2SharpException ex) when (ex.Message.Contains("network") || ex.Message.Contains("timeout"))
                {
                    _logger.LogError(ex, "Network error during push");
                    return GitOperationResult.Fail("Network error", new GitOperationError
                    {
                        Type = GitErrorType.NetworkError,
                        Details = "Unable to reach remote repository",
                        Exception = ex
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error during push");
                    return GitOperationResult.Fail("Push failed", new GitOperationError
                    {
                        Type = GitErrorType.Unknown,
                        Details = ex.Message,
                        Exception = ex
                    });
                }
            }, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<GitOperationResult> GetUnpushedCommitsAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                EnsureRepository();

                var localBranch = _repository.Head;
                if (localBranch.Tip == null)
                {
                    return GitOperationResult.Ok("No commits in local branch", new List<GitCommitInfo>());
                }

                var trackedBranch = localBranch.TrackedBranch;
                if (trackedBranch == null)
                {
                    _logger.LogWarning("No remote tracking branch configured");
                    return GitOperationResult.Fail("No remote tracking branch", new GitOperationError
                    {
                        Type = GitErrorType.InvalidOperation,
                        Details = "Branch is not tracking a remote branch"
                    });
                }

                var filter = new CommitFilter
                {
                    IncludeReachableFrom = localBranch.Tip,
                    ExcludeReachableFrom = trackedBranch.Tip,
                    SortBy = CommitSortStrategies.Time | CommitSortStrategies.Reverse
                };

                var unpushedCommits = _repository.Commits.QueryBy(filter)
                    .Select(c => new GitCommitInfo
                    {
                        Sha = c.Sha,
                        ShortSha = c.Sha.Substring(0, 7),
                        Message = c.Message,
                        MessageShort = c.MessageShort,
                        AuthorName = c.Author.Name,
                        AuthorEmail = c.Author.Email,
                        Date = c.Author.When,
                        CommitterName = c.Committer.Name,
                        CommitterEmail = c.Committer.Email,
                        CommitDate = c.Committer.When
                    })
                    .ToList();

                _logger.LogInformation("Found {Count} unpushed commits", unpushedCommits.Count);
                return GitOperationResult.Ok($"Found {unpushedCommits.Count} unpushed commits", unpushedCommits);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unpushed commits");
                return GitOperationResult.Fail("Failed to get unpushed commits", new GitOperationError
                {
                    Type = GitErrorType.Unknown,
                    Details = ex.Message,
                    Exception = ex
                });
            }
        }, cancellationToken);
    }

    public Task<GitOperationResult> GetCommitHistoryAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        _repository?.Dispose();
        _lock?.Dispose();
    }

    #region Helpers

    private void EnsureRepository()
    {
        if (_repository == null)
        {
            if (Repository.IsValid(_options.LocalRepositoryPath))
            {
                _repository = new Repository(_options.LocalRepositoryPath);
            }
            else
            {
                throw new InvalidOperationException(
                    "Repository not initialized. Call CloneAsync first.");
            }
        }
    }

    private CredentialsHandler GetCredentialsHandler()
    {
        return _options.CredentialType switch
        {
            GitCredentialType.Token => (url, user, cred) =>
                new UsernamePasswordCredentials
                {
                    Username = _options.PersonalAccessToken,
                    Password = string.Empty
                },

            GitCredentialType.UsernamePassword => (url, user, cred) =>
                new UsernamePasswordCredentials
                {
                    Username = _options.Username,
                    Password = _options.Password
                },

            _ => (url, user, cred) => new DefaultCredentials()
        };
    }

    #endregion
}
