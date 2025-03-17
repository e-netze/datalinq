using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace E.DataLinq.Web.Extensions;

static class GeneralExtensions
{
    static public string ExpandoToCsv(this object[] objects, string separator = ";", bool header = true)
    {
        StringBuilder sb = new StringBuilder();
        if (objects != null)
        {
            foreach (var rec in objects)
            {
                if (rec is IDictionary<string, object>)
                {
                    IDictionary<string, object> record = rec as IDictionary<string, object>;
                    if (record != null)
                    {
                        foreach (var column in record.Keys.ToArray())
                        {
                            if (column.StartsWith("_") || column == "oid")
                            {
                                record.Remove(column);
                            }
                        }

                        if (header)
                        {
                            sb.Append(String.Join(separator, record.Keys) + Environment.NewLine);
                            header = false;
                        }
                        sb.Append(String.Join(separator, record.Values.Select(v => "\"" + v.ToString().Replace("\"", "\"\"") + "\"")) + Environment.NewLine);
                    }
                }
            }
        }

        return sb.ToString();
    }

    static public string GetLine(this string text, int lineNumber)
    {
        int i = 0;
        using (StringReader sr = new StringReader(text))
        {
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                if (++i == lineNumber)
                {
                    return line;
                }
            }
        }

        return String.Empty;
    }
}
