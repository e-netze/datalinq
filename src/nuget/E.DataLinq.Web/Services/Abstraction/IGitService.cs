using E.DataLinq.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services.Abstraction;

public interface IGitService
{
    bool IsEnabled { get; }
    Task<GitOperationResult> CloneAsync(CancellationToken cancellationToken = default);
    Task<GitOperationResult> PullAsync(CancellationToken cancellationToken = default);
    Task<GitOperationResult> CommitAsync(string message, string userName = "", CancellationToken cancellationToken = default);
    Task<GitOperationResult> PushAsync(CancellationToken cancellationToken = default);
    Task<GitOperationResult> CommitAndPushAsync(string message, string userName = "", CancellationToken cancellationToken = default);
    Task<GitOperationResult> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<GitOperationResult> GetUnpushedCommitsAsync(CancellationToken cancellationToken = default);
    Task<GitOperationResult> GetCommitHistoryAsync(int count = 10, CancellationToken cancellationToken = default);
}
