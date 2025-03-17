using System;

namespace E.DataLinq.Core.Exceptions;

public class InvalidTokenException : Exception
{
    public InvalidTokenException(string message = "")
        : base(message) { }
}
