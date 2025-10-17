using E.DataLinq.Core.Engines.Abstraction;
using E.DataLinq.Core.Extensions;
using E.DataLinq.Core.Models;
using E.DataLinq.Core.Services.Abstraction;
using Microsoft.Extensions.Hosting;
using Neo4j.Driver;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace E.DataLinq.Core.Engines;

public class CypherEngine : IDataLinqSelectEngine
{
    private readonly IDataLinqEnvironmentService _environment;

    public CypherEngine(IDataLinqEnvironmentService environment)
    {
        _environment = environment;
    }
    public int EndpointType => (int)DefaultEndPointTypes.Cypher;

    public async Task<(object[] records, bool isOrdered)> SelectAsync(DataLinqEndPoint endPoint, DataLinqEndPointQuery query, NameValueCollection arguments)
    {
        if (string.IsNullOrEmpty(query.Statement))
            return (Array.Empty<object>(), false);

        string cypher = query.Statement.ParseStatement(arguments, StatementType.Sql);

        if (!AreParametersMatching(cypher, arguments))
            throw new Exception("Parameters arent matching!");

        var connection = CypherConnection.FromConnectionString(endPoint.GetConnectionString(_environment));

        try
        {
            await using var driver = GraphDatabase.Driver(connection.Url, AuthTokens.Basic(connection.Username, connection.Password));

            var parameters = new Dictionary<string, object>();
            foreach (string key in arguments)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    parameters[key] = arguments[key];
                }
            }

            var res = await driver.ExecutableQuery(cypher).WithParameters(parameters).WithConfig(new QueryConfig(database: connection.Database)).ExecuteAsync();

            var list = convertToExpando(res);

            return (list.ToArray(), false);
        }
        catch (Neo4jException ex)
        {
            throw new ApplicationException("Neo4j query execution error.", ex);
        }
        catch (Exception ex)
        {
            throw;
        }
    }


    public async Task<bool> TestConnection(DataLinqEndPoint endPoint)
    {
        var connection = CypherConnection.FromConnectionString(endPoint.GetConnectionString(_environment));

        await using var driver = GraphDatabase.Driver(connection.Url, AuthTokens.Basic(connection.Username, connection.Password));

        return await driver.TryVerifyConnectivityAsync();
    }


    #region Helpers

    private List<object> convertToExpando(EagerResult<IReadOnlyList<IRecord>> input)
    {
        var list = new List<object>();

        var records = input.Result;

        foreach (var record in records)
        {
            dynamic expando = new ExpandoObject();
            var dict = (IDictionary<string, object>)expando;

            foreach (var key in record.Keys)
            {
                var val = record[key];

                if (val is INode node)
                {
                    dict[$"elementId"] = node.ElementId;

                    dict[$"identity"] = node.ElementId.Split(':')[2];

                    foreach (var kvp in node.Properties)
                    {
                        dict[kvp.Key] = kvp.Value;
                    }
                }
                else
                {
                    dict[key] = val;
                }
            }

            list.Add(expando);
        }
        return list;
    }

    bool AreParametersMatching(string cypherQuery, NameValueCollection arguments)
    {
        if (string.IsNullOrWhiteSpace(cypherQuery))
            return false;

        var paramNames = Regex.Matches(cypherQuery, @"\$(\w+)")
                              .Select(m => m.Groups[1].Value)
                              .Where(name => name != "_pjson")
                              .Distinct();

        return paramNames.All(name => !string.IsNullOrWhiteSpace(arguments[name]));
    }

}

class CypherConnection
{
    public string Url { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string Database { get; set; }

    public static CypherConnection FromConnectionString(string connectionString)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var dict = parts
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim().ToLower(), p => p[1].Trim());

        return new CypherConnection
        {
            Url = dict.GetValueOrDefault("url") ?? dict.GetValueOrDefault("server"),
            Username = dict.GetValueOrDefault("username") ?? dict.GetValueOrDefault("user"),
            Password = dict.GetValueOrDefault("password") ?? dict.GetValueOrDefault("pwd"),
            Database = dict.GetValueOrDefault("database") ?? dict.GetValueOrDefault("db"),
        };
    }
}
#endregion