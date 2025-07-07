using E.DataLinq.Core.Engines.Abstraction;
using E.DataLinq.Core.Extensions;
using E.DataLinq.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public class JsonApiEngine : IDataLinqSelectEngine
{
    private readonly ILogger<JsonApiEngine> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public JsonApiEngine(
        ILogger<JsonApiEngine> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public int EndpointType => (int)DefaultEndPointTypes.JsonApi;

    public async Task<bool> TestConnection(DataLinqEndPoint endPoint)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(endPoint.ConnectionString);

            return response != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JsonApiEngine.TestConnection failed");
            return false;
        }
    }

    public async Task<(object[] records, bool isOrdered)> SelectAsync(
        DataLinqEndPoint endPoint,
        DataLinqEndPointQuery query,
        NameValueCollection arguments)
    {
        try
        {
            string statementWithReplacements = query.Statement;

            foreach (string key in arguments.AllKeys.Where(k => !k.StartsWith("_")))
            {
                string placeholder = "@" + key;
                string value = arguments[key] ?? string.Empty;
                statementWithReplacements = statementWithReplacements.Replace(placeholder, value);
            }

            var fullUrl = BuildUrl(endPoint.ConnectionString, statementWithReplacements, arguments);
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(fullUrl);


            if (!response.IsSuccessStatusCode)
                throw new Exception($"HTTP request failed with status {response.StatusCode}");

            var content = await response.Content.ReadAsStringAsync();

            JsonNode? rootNode = JsonNode.Parse(content);
            if (rootNode is null)
                throw new Exception("Failed to parse JSON response");

            JsonNode resultData = ExtractJsonResult(rootNode, arguments);

            ExpandoObject[] records = resultData switch
            {
                JsonArray arr => arr.Select(JsonNodeToExpando).ToArray(),
                _ => new ExpandoObject[] { JsonNodeToExpando(resultData) }
            };

            var marker = new ExpandoObject() as IDictionary<string, object>;
            marker["IsJsonApiResponse"] = true;

            var markedRecords = new ExpandoObject[] { (ExpandoObject)marker }
                                .Concat(records)
                                .ToArray();

            return (markedRecords, false);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JsonApiEngine.SelectAsync failed");
            throw;
        }
    }

    ExpandoObject JsonNodeToExpando(JsonNode node)
    {
        var expando = new ExpandoObject();
        var dict = (IDictionary<string, object>)expando;

        if (node is JsonObject obj)
        {
            foreach (var kvp in obj)
            {
                dict[kvp.Key] = ConvertJsonNodeValue(kvp.Value);
            }
        }
        else
        {
            throw new ArgumentException("Expected a JsonObject");
        }

        return expando;
    }

    object ConvertJsonNodeValue(JsonNode? node)
    {
        if (node == null)
            return null!;

        return node switch
        {
            JsonObject o => JsonNodeToExpando(o),
            JsonArray a => a.Select(ConvertJsonNodeValue).ToList(),
            JsonValue v => v.GetValue<object>() ?? null!,
            _ => node.ToString() ?? null!
        };
    }

    private static string BuildUrl(string baseUrl, string queryString, NameValueCollection args)
    {
        queryString = queryString.ParseStatement(args).ReplacePlaceholders(args);

        if (string.IsNullOrWhiteSpace(queryString))
            return baseUrl;

        if (queryString.StartsWith('?') || baseUrl.EndsWith('/'))
            return baseUrl + queryString;

        return baseUrl + "?" + queryString;
    }

    private static JsonNode ExtractJsonResult(JsonNode root, NameValueCollection args)
    {
        var selector = args["path"];
        if (string.IsNullOrWhiteSpace(selector))
            return root;

        var tokens = selector
            .TrimStart('$', '.')
            .Split('.', StringSplitOptions.RemoveEmptyEntries);

        JsonNode? current = root;
        foreach (var token in tokens)
        {
            if (current is JsonObject obj && obj.TryGetPropertyValue(token, out var next))
            {
                current = next;
            }
            else
            {
                throw new Exception($"Path '{selector}' not found in JSON.");
            }
        }

        return current ?? throw new Exception($"Path '{selector}' resulted in null.");
    }

    private static JsonNode? ConvertAllValuesToStrings(JsonNode? node)
    {
        if (node == null)
            return null;

        switch (node)
        {
            case JsonObject obj:
                var newObj = new JsonObject();
                foreach (var kvp in obj)
                {
                    newObj[kvp.Key] = ConvertAllValuesToStrings(kvp.Value);
                }
                return newObj;

            case JsonArray arr:
                var newArr = new JsonArray();
                foreach (var item in arr)
                {
                    newArr.Add(ConvertAllValuesToStrings(item));
                }
                return newArr;

            case JsonValue val:
                if (val.TryGetValue<string>(out var s)) return JsonValue.Create(s);
                if (val.TryGetValue<int>(out var i)) return JsonValue.Create(i.ToString());
                if (val.TryGetValue<bool>(out var b)) return JsonValue.Create(b.ToString());
                if (val.TryGetValue<double>(out var d)) return JsonValue.Create(d.ToString());
                if (val.TryGetValue<float>(out var f)) return JsonValue.Create(f.ToString());
                return JsonValue.Create(val.ToString());

            default:
                return node;
        }
    }

}
