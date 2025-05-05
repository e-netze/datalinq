#nullable enable

using E.DataLinq.Core.Services.Persistance.Abstraction;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services;
public class DataLinqCodeReflectionService
{
    private readonly ILogger<DataLinqCodeReflectionService> _logger;
    private readonly IPersistanceProviderService _persistanceProvider;
    
    public DataLinqCodeReflectionService(
        ILogger<DataLinqCodeReflectionService> logger,
        IPersistanceProviderService persistanceProvider)
    {
        _logger = logger;
        _persistanceProvider = persistanceProvider;
    }

    async public Task<string> CodeReflectionBlocks(string id, string code, bool appendAssets)
    {
        var markedBlocks = code.ExtractMarkedBlocks();
        if(markedBlocks.Count == 0)
        {
            return String.Empty;
        }

        var dataLinqRoute = new DataLinqRoute(id, null);
        StringBuilder sb = new StringBuilder();
        
        sb.Append(@"<script type=""text/javascript"">");
        sb.Append("dataLinq._codeReflectionBlocks=[];");

        List<string>? assets = appendAssets ? new([ $"{dataLinqRoute.EndpointId}@{dataLinqRoute.QueryId}" ]) : null;

        foreach (var margedBlock in markedBlocks)
        {
            await AppendCodeBlock(sb, margedBlock.Key, margedBlock.Value, assets);
        }

        #region append all quries

        //if (appendAssets)
        //{

        //    var viewQuery = await _persistanceProvider.GetEndPointQuery(dataLinqRoute.EndpointId, dataLinqRoute.QueryId);
        //    AppendCodeBlock(sb, $"asset:{dataLinqRoute.EndpointId}@{dataLinqRoute.QueryId}", viewQuery.Statement);

        //    foreach (var queryId in await _persistanceProvider.GetQueryIds(dataLinqRoute.EndpointId))
        //    {
        //        var query = await _persistanceProvider.GetEndPointQuery(dataLinqRoute.EndpointId, queryId);

        //        AppendCodeBlock(sb, $"asset:{dataLinqRoute.EndpointId}@{queryId}", query.Statement);
        //    }
        //}

        #endregion

        sb.Append("</script>");

        return sb.ToString();
    }

    #region Helper

    async private Task AppendCodeBlock(StringBuilder sb, string id, string content, List<string>? assets)
    {
        (string code, List<string> comments) = content.ExtractAndRemoveHtmlComments();

        string description = String.Join(Environment.NewLine, comments).ExtractLanguage("en");
        assets?.AddRange(code.ExtractStringLiteralsWithAt());

        sb.Append("dataLinq._codeReflectionBlocks.push({");
        sb.Append($"id:'{id}'");
        sb.Append($",code:'{EncodeCodeBlockContent(code)}'");
        sb.Append($",description:'{EncodeCodeBlockContent(description)}'");
        sb.Append($",assets:[");

        bool firstAsset = true;
        foreach(var asset in assets ?? [])
        {
            var dataLinqRoute = new DataLinqRoute(asset, null);

            if(dataLinqRoute.HasQuery && !dataLinqRoute.HasView)
            {
                var query = await _persistanceProvider.GetEndPointQuery(dataLinqRoute.EndpointId, dataLinqRoute.QueryId.Split("?")[0]);
                if(query is not null)
                {
                    if (!firstAsset) sb.Append(",");

                    sb.Append("{");
                    sb.AppendLine($"id:'{dataLinqRoute.EndpointId}@{dataLinqRoute.QueryId}'");
                    sb.AppendLine($",code:'{EncodeCodeBlockContent(query.Statement)}'");
                    sb.Append("}");

                    firstAsset = false;
                }
            }
        }

        sb.Append("]"); // assets
        sb.Append("});");
    }

    private string EncodeCodeBlockContent(string content)
        => Convert.ToBase64String(Encoding.Unicode.GetBytes(content.Replace("\r", "")));


    #endregion
}
