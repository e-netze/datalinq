using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;

namespace E.DataLinq.Core.Extensions;

public enum StatementType
{
    Sql,
    Url
}

static class DataLinqExtensoins
{
    static public string ParseStatement(this string statement, NameValueCollection nvc, StatementType statementType = StatementType.Sql)
    {
        if (!statement.Contains("#if "))
        {
            return statement;
        }

        var stringReader = new StringReader(statement);

        StringBuilder sb = new StringBuilder();
        string statementLine;
        int level = 0;
        bool[] levelContition = new bool[100].Select(b => true).ToArray();

        while ((statementLine = stringReader.ReadLine()) != null)
        {
            statementLine = statementLine.Trim();
            if (statementLine.StartsWith("#if "))
            {
                level++;

                levelContition[level] = levelContition[level - 1];
                if (levelContition[level] == true)
                {
                    foreach (string parameter in statementLine.Substring(4).Trim().Split(','))
                    {
                        if (!nvc.AllKeys.Contains(parameter) || String.IsNullOrWhiteSpace(nvc[parameter]))
                        {
                            levelContition[level] = false;
                            break;
                        }
                    }
                }
            }
            else if (statementLine.StartsWith("#endif"))
            {
                level--;
                if (level < 0)
                {
                    throw new Exception("ParseStatement: Syntax error");
                }
            }
            else
            {
                if (levelContition[level] == true)
                {
                    switch (statementType)
                    {
                        case StatementType.Sql:
                            if (sb.Length > 0)
                            {
                                sb.Append(Environment.NewLine);
                            }

                            sb.Append(statementLine);
                            break;
                        case StatementType.Url:
                            if (!String.IsNullOrWhiteSpace(statementLine))
                            {
                                sb.Append(statementLine.Trim());
                            }

                            break;
                    }
                }
            }
        }

        if (level != 0)
        {
            throw new Exception("ParseStatement: Syntax error");
        }

        return sb.ToString();
    }

}
