using E.DataLinq.Core.Engines.Abstraction;
using E.DataLinq.Core.Services.Abstraction;
using System.Collections.Generic;

namespace E.DataLinq.Core.Extensions;

static class EngineFieldParserExtensions
{
    static public object ParseAny(this IEnumerable<IEngineFieldParserService> parsers, IRecordReader reader, string columnName)
    {
        if (parsers != null)
        {
            foreach (var parser in parsers)
            {
                var obj = parser.Parse(reader, columnName);
                if (obj != null)
                {
                    return obj;
                }
            }
        }

        return null;
    }
}
