using E.DataLinq.Web.Services.Abstraction;
using E.DataLinq.Web.Services.Agents;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Orchestration;
using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Linq;
using System.Threading.Tasks;


#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
public class SemanticKernelService
{
    private readonly ISemanticKernelFactory _skFactory;
    private readonly DataLinqAgentFactory _dataLinqAgentFactory;
    private readonly IAgent<string[], string> _userHistorySummarizerAgent;
    ChatHistory history = [];
    public SemanticKernelService(ISemanticKernelFactory skFactory, IAgent<string[], string> userHistorySummarizerAgent, DataLinqAgentFactory dataLinqAgentFactory)
    {
        _skFactory = skFactory;
        _userHistorySummarizerAgent = userHistorySummarizerAgent;
        _dataLinqAgentFactory = dataLinqAgentFactory;
    }

    public async Task<string> ProcessAsync(string[] userChatHistory)
    {
        Kernel kernel = _skFactory.CreateKernel();

        var dataLinqCodeAgent = _dataLinqAgentFactory.CreateAgent("DataLinqCodeAgent", "DataLinq View and Code expert agent.");
        var dataLinqQueryAgent = _dataLinqAgentFactory.CreateAgent("DataLinqQueryAgent", "DataLinq Query and Database expert agent.");
        var dataLinqEndpointAgent = _dataLinqAgentFactory.CreateAgent("DataLinqEndpointAgent", "DataLinq Endpoint and parameterization expert agent.");
        var dataLinqOrchestrator = new CustomGroupChatManager
        {
            MaximumInvocationCount = 5,
            UserHistory = userChatHistory,
            Kernel = kernel
        };

        GroupChatOrchestration orchestration = new GroupChatOrchestration(
            dataLinqOrchestrator,
            dataLinqCodeAgent, 
            dataLinqQueryAgent, 
            dataLinqEndpointAgent)
        {
            ResponseCallback = responseCallback,
        };

        InProcessRuntime runtime = new InProcessRuntime();
        await runtime.StartAsync();

        var result = await orchestration.InvokeAsync(await _userHistorySummarizerAgent.RunAsync(userChatHistory), runtime);

        string output = await result.GetValueAsync(TimeSpan.FromSeconds(60));

        return output;
    }

    ValueTask responseCallback(ChatMessageContent response)
    {
        history.Add(response);
        return ValueTask.CompletedTask;
    }
}