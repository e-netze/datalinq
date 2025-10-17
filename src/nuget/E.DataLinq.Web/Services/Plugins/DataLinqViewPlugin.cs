using E.DataLinq.Core.Services.Persistance.Abstraction;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static E.DataLinq.Web.Services.CopilotReflectionInitializer;

namespace E.DataLinq.Web.Services.Plugins;

public class DataLinqViewPlugin
{
    private readonly IPersistanceProviderService _persistanceProvider;

    public DataLinqViewPlugin(IPersistanceProviderService persistanceProvider)
    {
        _persistanceProvider = persistanceProvider;
    }


    [KernelFunction("get_datalinq_endpoint_query_view")]
    [Description("Gets the view code for a specific datalinq id consisting of endpoint,query,view devided by @")]
    [return: Description("The code for the DataLinq view")]
    public async Task<string> GetDataLinqEndPointQueryView(
    [Description("The endpoint id of the datalinq identifier")]
    string part1,
    [Description("The query id of the datalinq identifier")]
    string part2,
    [Description("The view id of the datalinq identifier")]
    string part3)
    {
        if (string.IsNullOrWhiteSpace(part1))
            throw new ArgumentException("Endpoint cannot be null or empty", nameof(part1));

        if (string.IsNullOrWhiteSpace(part2))
            throw new ArgumentException("Query cannot be null or empty", nameof(part2));

        if (string.IsNullOrWhiteSpace(part2))
            throw new ArgumentException("View cannot be null or empty", nameof(part3));

        try
        {
            var model = await _persistanceProvider.GetEndPointQueryView(part1, part2, part3);
            return model != null ? model.Code : string.Empty;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to retrieve DataLinq endpoint query view for '{part1}'@'{part2}'@'{part3}", ex);
        }
    }
}
