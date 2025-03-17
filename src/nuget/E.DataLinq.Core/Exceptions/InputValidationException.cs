using System;

namespace E.DataLinq.Core.Exceptions;

public class InputValidationException : Exception
{
    public InputValidationException(string message)
        : base(message)
    {

    }
}
