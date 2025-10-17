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

public class DataLinqEndpointPlugin
{
    private readonly IPersistanceProviderService _persistanceProvider;

    public DataLinqEndpointPlugin(IPersistanceProviderService persistanceProvider)
    {
        _persistanceProvider = persistanceProvider;
    }

    [KernelFunction("get_datalinq_endpoint")]
    [Description("Gets the endpoint configuration for a specific datalinq id consisting of endpoint")]
    [return: Description("The endpoint definition or configuration for the DataLinq identifier")]
    public async Task<string> GetDataLinqEndPoint(
        [Description("The datalinq endpoint identifier")]
    string part1)
    {
        if (string.IsNullOrWhiteSpace(part1))
            throw new ArgumentException("Part1 cannot be null or empty", nameof(part1));

        try
        {
            var model = await _persistanceProvider.GetEndPoint(part1);
            return model != null ? JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true }) : string.Empty;

        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to retrieve DataLinq endpoint for '{part1}'", ex);
        }
    }

}
