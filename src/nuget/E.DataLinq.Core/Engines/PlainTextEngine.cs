using E.DataLinq.Core.Engines.Abstraction;
using E.DataLinq.Core.Extensions;
using E.DataLinq.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace E.DataLinq.Core.Engines;

public class PlainTextEngine : IDataLinqSelectEngine
{
    public int EndpointType => (int)DefaultEndPointTypes.PlainText;

    public Task<bool> TestConnection(DataLinqEndPoint endPoint)
    {
        return Task.FromResult(true);
    }

    async public Task<(object[] records, bool isOrdered)> SelectAsync(DataLinqEndPoint endPoint, DataLinqEndPointQuery query, NameValueCollection arguments)
    {
        bool isOrdered = false;
        int level = 0;

        var resultCollection = await ParseStatement(query.Statement);

        while (arguments.AllKeys.Contains($"level{level}"))
        {
            var levelArgumrent = arguments[$"level{level++}"];

            resultCollection = resultCollection
                .Where(i => i.value == levelArgumrent)
                .FirstOrDefault()?
                .ChildRecords;

            if (resultCollection == null)
            {
                break;
            }
        }

        if (!String.IsNullOrEmpty(arguments["value"]))
        {
            var filter = arguments["value"];
            var options = RegexOptions.None;

            if (filter.Contains("%"))
            {
                filter = filter.Replace("%", "*");
                options |= RegexOptions.IgnoreCase;
            }
            filter = filter.WildcardToRegex();

            resultCollection = resultCollection?.Where(i => Regex.IsMatch(i.value, filter, options));
        }

        var records = resultCollection?.Select(r => r.ToObject()).ToArray();

        return (records ?? new object[0], isOrdered);
    }

    #region Helper

    async private Task<IEnumerable<Record>> ParseStatement(string statement)
    {
        ICollection<Record> result = new List<Record>(), current = result;
        int currentLevel = 0;

        using (var reader = new StringReader(statement))
        {
            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line) || line.Trim().StartsWith("## "))
                {
                    continue;
                }

                int level = GetLevel(line);

                if (currentLevel < level)
                {
                    current.Last().ChildRecords = current.Last().ChildRecords ?? new List<Record>();
                    current = current.Last().ChildRecords;
                    currentLevel = level;
                }
                else if (currentLevel > level)
                {
                    current = result;
                    for (var i = 0; i < level; i++)
                    {
                        current = current.Last().ChildRecords;
                    }
                    currentLevel = level;
                }

                current.Add(ParseLine(line));
            }
        }

        return result.ToArray();
    }

    private Record ParseLine(string line)
    {
        if (line.Contains(":"))
        {
            return new Record()
            {
                value = line.Substring(0, line.IndexOf(":")).Trim(),
                name = line.Substring(line.IndexOf(":") + 1).Trim()
            };
        }

        return new Record() { name = line.Trim(), value = line.Trim() };
    }

    private int GetLevel(string line)
    {
        int level = 0;

        line = line.Replace("\t", "  ");

        while (line.StartsWith("  "))
        {
            line = line.Substring(2);
            level++;
        }

        return level;
    }

    #endregion

    #region Models

    private class Record
    {
        public string value { get; set; }
        public string name { get; set; }

        public ICollection<Record> ChildRecords { get; set; }

        public object ToObject()
        {
            ExpandoObject expando = new ExpandoObject();
            IDictionary<string, object> expandoDict = (IDictionary<string, object>)expando;

            expandoDict["value"] = this.value;
            expandoDict["name"] = this.name;

            return expando;
        }
    }

    #endregion
}
