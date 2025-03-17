using E.DataLinq.Core.Models;
using System;
using System.Collections.Generic;

namespace E.DataLinq.Core.Exceptions;

public class RazorCompileException : Exception
{
    public RazorCompileException() : base() { }
    public RazorCompileException(string message) : base(message) { }

    public IEnumerable<RazorCompileError> CompilerErrors { get; set; }
}
