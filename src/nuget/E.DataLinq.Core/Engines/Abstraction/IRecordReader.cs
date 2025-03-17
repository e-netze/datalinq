using System;

namespace E.DataLinq.Core.Engines.Abstraction;

public interface IRecordReader : IDisposable
{
    object GetValue(string columnName);
    bool HasCoumn(string columnName);
}
