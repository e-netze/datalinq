using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Services.Abstraction;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
public class CustomGroupChatManager : GroupChatManager
{
    public string[] UserHistory { get; set; }
    public Kernel Kernel { get; set; }
    private List<string> previousAgents = new();
    public async override ValueTask<GroupChatManagerResult<string>> FilterResults(ChatHistory history, CancellationToken cancellationToken = default)
    {
        var jsonContent = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });

        var result = await Kernel.InvokePromptAsync($"""
            You are a summarizing agent. Return a summarization of the agent chat history to answer the users question.

            User question:
            {UserHistory.Last()}

            Agent chat history:
            {jsonContent}

            Rules:
            1. Summarize the chat history to which the Agents contributed.
            2. Make sure to include the complete context and all the details which might be important.
            3. It is better to answer with a longer and detailed answer than a short one with missing data.
            4. Always include the relevant Code and Query snippets if present in the history.
            """);

        return new GroupChatManagerResult<string>(result.ToString()?.Trim() ?? string.Empty) { Reason = "Custom selection logic." };
    }

    public async override ValueTask<GroupChatManagerResult<string>> SelectNextAgent(ChatHistory history, GroupChatTeam team, CancellationToken cancellationToken = default)
    {
        var jsonContent = JsonSerializer.Serialize(UserHistory, new JsonSerializerOptions { WriteIndented = true });
        var jsonContent1 = JsonSerializer.Serialize(previousAgents, new JsonSerializerOptions { WriteIndented = true });

        var result = await Kernel.InvokePromptAsync($"""
            You are a routing agent. Return only the name of the next participant who should speak.

            Participants:
             - DataLinqGeneralAgent
             - DataLinqCodeAgent
             - DataLinqQueryAgent
             - DataLinqEndpointAgent

            Rules:
            1. If the user has general questions regarding DataLinq → choose DataLinqGeneralAgent.
            2. If the user has a problem/question regarding a DataLinq View, HTML, CSS, VanillaJS, C# Razor, or DataLinq Helper functions → choose DataLinqCodeAgent.
            3. If the user has a problem/question regarding a DataLinq Query, SQL, SQLite, Oracle, or Postgres → choose DataLinqQueryAgent.
            4. If the user has a problem/question regarding a DataLinq Endpoint and its values or parameterization → choose DataLinqEndpointAgent.
            5. If you are unsure → choose DataLinqGeneralAgent.

            Selection Logic:

            Always select only one agent.
            If multiple agents are relevant, select the most important one.
            If the most important agent has already spoken (is listed in Previous Agents), select the next most relevant agent.
            If all relevant agents have already spoken, return "Finished".

            History:
            {jsonContent}

            Previous Agents:
            {jsonContent1}

            Output Format:
            Return only the name of the next agent (e.g., DataLinqCodeAgent) or "Finished".
            """);

        previousAgents.Add(result.ToString()?.Trim());

        return new GroupChatManagerResult<string>(result.ToString()?.Trim() ?? string.Empty) { Reason = "Custom selection logic." };
    }

    public override ValueTask<GroupChatManagerResult<bool>> ShouldRequestUserInput(ChatHistory history, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(new GroupChatManagerResult<bool>(false) { Reason = "No user input required." });
    }

    public override ValueTask<GroupChatManagerResult<bool>> ShouldTerminate(ChatHistory history, CancellationToken cancellationToken = default)
    {
        var baseResult = base.ShouldTerminate(history, cancellationToken).Result;
        if (baseResult.Value)
        {
            return ValueTask.FromResult(baseResult);
        }

        bool shouldEnd = history.Last().Equals("Finished");
        return ValueTask.FromResult(new GroupChatManagerResult<bool>(shouldEnd) { Reason = "Custom termination logic." });
    }
}