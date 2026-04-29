using System;
using System.Collections.Generic;
using System.Text;

namespace E.DataLinq.Core.Models;

public class GitCommitChangesRequest
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Message { get; set; }
}
