using System;
using System.Collections.Generic;
using System.Text;

namespace E.DataLinq.Core.Models;

public class GitCommitInfo
{
    public string Sha { get; set; } = string.Empty;
    public string ShortSha { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string MessageShort { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorEmail { get; set; } = string.Empty;
    public DateTimeOffset Date { get; set; }
    public string CommitterName { get; set; } = string.Empty;
    public string CommitterEmail { get; set; } = string.Empty;
    public DateTimeOffset CommitDate { get; set; }
    public int ParentCount { get; set; }
}
