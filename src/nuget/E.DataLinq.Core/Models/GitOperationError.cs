using E.DataLinq.Core.Models.Abstraction;
using System;
using System.Collections.Generic;
using System.Text;

namespace E.DataLinq.Core.Models;

public class GitOperationError
{
    public GitErrorType Type { get; set; }
    public string Details { get; set; } = string.Empty;
    public Exception Exception { get; set; }
}
