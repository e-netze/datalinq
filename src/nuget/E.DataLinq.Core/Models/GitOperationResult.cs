using System;
using System.Collections.Generic;
using System.Text;

namespace E.DataLinq.Core.Models;

public class GitOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public GitOperationError Error { get; set; }
    public object Data { get; set; }

    public static GitOperationResult Ok(string message = null, object data = null) =>
        new() { Success = true, Message = message, Data = data };

    public static GitOperationResult Fail(string message, GitOperationError error) =>
        new() { Success = false, Message = message, Error = error };
}
