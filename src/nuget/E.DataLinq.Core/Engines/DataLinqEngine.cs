using E.DataLinq.Core.Engines.Abstraction;
using E.DataLinq.Core.Extensions;
using E.DataLinq.Core.Models;
using E.DataLinq.Core.Services.Abstraction;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Dynamic;
using System.Net.Http;
using System.Threading.Tasks;

namespace E.DataLinq.Core.Engines;

public class DataLinqEngine : IDataLinqSelectEngine
{
    private static HttpClient _httpClient;
    private readonly IDataLinqEnvironmentService _envrionment;

    public DataLinqEngine(IDataLinqEnvironmentService envrionment)
    {
        _envrionment = envrionment;

        // reuse http client...
        _httpClient = _httpClient ?? new HttpClient(
                new HttpClientHandler
                {
                    UseDefaultCredentials = true
                }
            );
    }

    public int EndpointType => (int)DefaultEndPointTypes.DataLinq;

    public Task<bool> TestConnection(DataLinqEndPoint endPoint)
    {
        throw new NotImplementedException();
    }

    async public Task<(object[] records, bool isOrdered)> SelectAsync(
                DataLinqEndPoint endPoint,
                DataLinqEndPointQuery query,
                NameValueCollection arguments)
    {
        string connectionString = endPoint.GetConnectionString(_envrionment);

        string url = connectionString + "/datalinq/select/" + query.Statement;
        foreach (var parameterName in arguments.AllKeys)
        {
            if (String.IsNullOrWhiteSpace(parameterName))
            {
                continue;
            }

            if (url.Contains("{{" + parameterName + "}}"))
            {
                url = url.Replace("{{" + parameterName + "}}", arguments[parameterName]);
            }
        }

        List<object> result = new List<object>();

        HttpResponseMessage response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode(); // Stellt sicher, dass der Statuscode erfolgreich ist

        string responseBody = await response.Content.ReadAsStringAsync();
        var records = JsonConvert.DeserializeObject<Dictionary<string, object>[]>(responseBody);

        if (records != null)
        {
            foreach (var record in records)
            {
                ExpandoObject expando = new ExpandoObject();
                IDictionary<string, object> expandoDict = (IDictionary<string, object>)expando;

                foreach (var key in record.Keys)
                {
                    if (key.StartsWith("_"))
                    {
                        continue;
                    }

                    expandoDict.Add(key, record[key]);
                }
                result.Add(expando);
            }
        }

        return (records: result.ToArray(), isOrdered: false);
    }
}
