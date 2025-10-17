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

public class DataLinqQueryPlugin
{
    private readonly IPersistanceProviderService _persistanceProvider;

    public DataLinqQueryPlugin(IPersistanceProviderService persistanceProvider)
    {
        _persistanceProvider = persistanceProvider;
    }

    [KernelFunction("get_datalinq_endpoint_query")]
    [Description("Gets the query statement for a specific datalinq id consisting of endpoint,query devided by @")]
    [return: Description("The query definition or configuration for the DataLinq endpoint")]
    public async Task<string> GetDataLinqEndPointQuery(
   [Description("The endpoint id of the datalinq identifier")]
    string part1,
    [Description("The query id of the datalinq identifier")]
    string part2)
    {
        if (string.IsNullOrWhiteSpace(part1))
            throw new ArgumentException("Part1 cannot be null or empty", nameof(part1));

        if (string.IsNullOrWhiteSpace(part2))
            throw new ArgumentException("Part2 cannot be null or empty", nameof(part2));

        try
        {
            var model = await _persistanceProvider.GetEndPointQuery(part1, part2);
            return model != null ? model.Statement : string.Empty;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to retrieve DataLinq endpoint query for '{part1}' and '{part2}'", ex);
        }
    }
}
