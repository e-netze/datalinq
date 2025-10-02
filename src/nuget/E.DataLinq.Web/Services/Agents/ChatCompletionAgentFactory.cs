using E.DataLinq.Web.Services.Abstraction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services.Agents;

public class DataLinqAgentFactory
{
    private readonly ISemanticKernelFactory _kernelFactory;
    private readonly IConfiguration _config;

    public DataLinqAgentFactory(ISemanticKernelFactory kernelFactory, IConfiguration config)
    {
        _kernelFactory = kernelFactory;
        _config = config;
    }

    public ChatCompletionAgent CreateAgent(string agentName, string agentDescription)
    {
        var path = _config[$"{AgentOptions.Key}:{agentName}"]
        ?? throw new InvalidOperationException($"No path configured for Agent:{agentName}");

        var instructions = File.ReadAllText(path);

        return new ChatCompletionAgent
        {
            Name = agentName,
            Description = agentDescription,
            Instructions = instructions,
            Kernel = _kernelFactory.CreateKernel(),
        };
    }
}
