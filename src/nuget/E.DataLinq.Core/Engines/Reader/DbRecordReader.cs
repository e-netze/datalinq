using E.DataLinq.Core.Engines.Abstraction;
using System;
using System.Data.Common;
using System.Linq;

namespace E.DataLinq.Core.Engines.Reader;

class DbRecordReader : IRecordReader
{
    private DbDataReader _reader;
    private string[] _columns;

    public DbRecordReader(DbDataReader reader)
    {
        _reader = reader;

        _columns = Enumerable.Range(0, reader.FieldCount)
                                        .Select(i => reader.GetName(i)?.ToLower())
                                        .ToArray();
    }

    public void Dispose()
    {
        _reader = null;
        _columns = null;
    }

    #region IRecordReader

    public object GetValue(string columnName)
    {
        return _reader[columnName];
    }

    public bool HasCoumn(string columnName)
    {
        return _columns.Contains(columnName?.ToLower());
    }

    #endregion
}
