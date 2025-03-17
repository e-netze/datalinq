using E.DataLinq.Core.Engines.Abstraction;

namespace E.DataLinq.Core.Services.Abstraction;

public interface IEngineFieldParserService
{
    public object Parse(IRecordReader reader, string columnName);
}
