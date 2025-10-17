using E.DataLinq.Web.Services.Abstraction;
using E.DataLinq.Web.Services.Agents;
using E.DataLinq.Web.Services.Plugins;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using System;
using System.Threading.Tasks;


#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
public class SemanticKernelService
{
    private readonly ISemanticKernelFactory _skFactory;
    private readonly IAgent<string[], string> _userHistorySummarizerAgent;
    private readonly ChatCompletionAgent _dataLinqCodeAgent;
    private readonly ChatCompletionAgent _dataLinqQueryAgent;
    private readonly ChatCompletionAgent _dataLinqEndpointAgent;
    private readonly ChatCompletionAgent _dataLinqGeneralAgent;

    public SemanticKernelService(
        ISemanticKernelFactory skFactory,
        IAgent<string[], string> userHistorySummarizerAgent,
        DataLinqAgentFactory dataLinqAgentFactory)
    {
        _skFactory = skFactory;
        _userHistorySummarizerAgent = userHistorySummarizerAgent;

        _dataLinqCodeAgent = dataLinqAgentFactory.CreateAgent("DataLinqCodeAgent","DataLinq View and Code expert agent.",typeof(DataLinqHelperFunctionsPlugin),typeof(DataLinqViewPlugin));
        _dataLinqQueryAgent = dataLinqAgentFactory.CreateAgent("DataLinqQueryAgent","DataLinq Query and Database expert agent.",typeof(DataLinqQueryPlugin));
        _dataLinqEndpointAgent = dataLinqAgentFactory.CreateAgent("DataLinqEndpointAgent","DataLinq Endpoint and parameterization expert agent.",typeof(DataLinqEndpointPlugin));
        _dataLinqGeneralAgent = dataLinqAgentFactory.CreateAgent("DataLinqGeneralAgent","DataLinq general information agent");
    }

    public async Task<string> ProcessAsync(string[] userChatHistory)
    {
        var kernel = _skFactory.CreateKernel();

        var dataLinqOrchestrator = new DataLinqCopilotOrchestrator
        {
            MaximumInvocationCount = 5,
            UserHistory = userChatHistory,
            Kernel = kernel
        };

        var orchestration = new GroupChatOrchestration(
            dataLinqOrchestrator,
            _dataLinqCodeAgent,
            _dataLinqQueryAgent,
            _dataLinqEndpointAgent,
            _dataLinqGeneralAgent);

        var userHistoryQuestionSummary = await _userHistorySummarizerAgent.RunAsync(userChatHistory);

        var runtime = new InProcessRuntime();
        await runtime.StartAsync();

        var result = await orchestration.InvokeAsync(userHistoryQuestionSummary, runtime);
        return await result.GetValueAsync(TimeSpan.FromSeconds(60));
    }
}