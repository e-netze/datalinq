using System;
using System.Collections.Generic;
using System.Text;

namespace E.DataLinq.Core.Models;

public class GitCommitChangesResult
{
    public bool Success { get; set; } = false;
    public string Message { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
