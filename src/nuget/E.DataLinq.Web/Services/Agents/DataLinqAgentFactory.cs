using E.DataLinq.Core.Services.Persistance.Abstraction;
using E.DataLinq.Web.Services.Abstraction;
using E.DataLinq.Web.Services.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System;
using System.IO;

namespace E.DataLinq.Web.Services.Agents;

public class DataLinqAgentFactory
{
    private readonly ISemanticKernelFactory _kernelFactory;
    private readonly IConfiguration _config;
    private readonly IServiceProvider _serviceProvider;


    public DataLinqAgentFactory(ISemanticKernelFactory kernelFactory, IConfiguration config, IServiceProvider serviceProvider)
    {
        _kernelFactory = kernelFactory;
        _config = config;
        _serviceProvider = serviceProvider;
    }

    public ChatCompletionAgent CreateAgent(string agentName, string agentDescription, params Type[] pluginTypes)
    {
        var path = _config[$"{AgentOptions.Key}:{agentName}"]
        ?? throw new InvalidOperationException($"No path configured for Agent:{agentName}");

        var instructions = File.ReadAllText(path);
        var kernel = _kernelFactory.CreateKernel();

        if (pluginTypes.Length > 0)
        {
            foreach (var pluginType in pluginTypes)
            {
                var pluginInstance = CreatePluginInstance(pluginType);
                var plugin = KernelPluginFactory.CreateFromObject(pluginInstance, pluginType.Name);
                kernel.Plugins.Add(plugin);
            }
        }

        return new ChatCompletionAgent
        {
            Name = agentName,
            Description = agentDescription,
            Instructions = instructions,
            Kernel = kernel,
            Arguments = new KernelArguments(
                new OpenAIPromptExecutionSettings()
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                })
        };
    }

    private object CreatePluginInstance(Type pluginType)
    {
        if (pluginType == typeof(DataLinqHelperFunctionsPlugin))
            return new DataLinqHelperFunctionsPlugin();

        using var scope = _serviceProvider.CreateScope();
        var persistanceService = scope.ServiceProvider.GetRequiredService<IPersistanceProviderService>();
        return Activator.CreateInstance(pluginType, persistanceService);
    }
}
