using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable SKEXP0110
public class DataLinqCopilotOrchestrator : GroupChatManager
{
    public string[] UserHistory { get; set; }
    public Kernel Kernel { get; set; }

    private List<string> previousAgents = new();
    private int iterationCount = 0;

    public async override ValueTask<GroupChatManagerResult<string>> FilterResults(ChatHistory history, CancellationToken cancellationToken = default)
    {
        var jsonContent = JsonSerializer.Serialize(history.Select(m => m.Content).ToList(), new JsonSerializerOptions { WriteIndented = true });

        var result = await Kernel.InvokePromptAsync($"""
            SYSTEM PROMPT: DataLinqSummarizerAgent (Lite)

            Goal:
            Read the multi-agent history and answer the latest user question clearly, using ONLY information found in the history.

            Inputs:
            - User question: {UserHistory.Last()}
            - Agent/chat history (JSON): {jsonContent}

            Instructions:
            1. First: Direct Answer to the user’s question.
            2. Then (only if non-trivial): a brief Context Summary section.
            3. Include any code / query / view snippets exactly as they appeared (label them). Do NOT invent or modify them.
            4. If a snippet is referenced but not shown, state: "Referenced snippet not present."
            5. Do not fabricate helpers, queries, features, access rules, or environments not in the history.
            6. If info is missing, clearly say what is unknown and (optionally) suggest: sandbox, official docs, or mail@xyz.com.
            7. If the user just greets or gives no real question, respond briefly and friendly—no large summary.
            8. If ambiguous intent: provide what you can + up to 2 clarifying questions.
            9. No external knowledge; no hallucination; no execution claims.
            10. Preserve important constraints (naming rules, agent boundaries, limitations).

            Format:
            - Use Markdown.
            - Sections (if needed): Answer, Context Summary, Snippets, Open Points, Next Steps.
            - End with an invitation for refinement if appropriate.

            Output ONLY the answer (no meta commentary about these rules).
            """);

        return new GroupChatManagerResult<string>(result.ToString()?.Trim() ?? string.Empty) { Reason = "Custom selection logic." };
    }

    public async override ValueTask<GroupChatManagerResult<string>> SelectNextAgent(ChatHistory history, GroupChatTeam team, CancellationToken cancellationToken = default)
    {
        var jsonContent = JsonSerializer.Serialize(UserHistory, new JsonSerializerOptions { WriteIndented = true });

        if (previousAgents.Count.Equals(0))
        {
            var result = await Kernel.InvokePromptAsync($"""
            You are the RoutingAgent. Given the full user chat history (JSON inserted below as {jsonContent}), select which participants should speak next.

            Participants (exact names):
            - DataLinqGeneralAgent
            - DataLinqEndpointAgent
            - DataLinqQueryAgent
            - DataLinqCodeAgent

            Decision Rules (apply in order, select all that match):
            1. DataLinqGeneralAgent: High-level platform structure, conceptual relationships (Endpoint ↔ Query ↔ View), UI workflow questions not requesting concrete syntax, ambiguous questions needing first clarification.
            2. DataLinqEndpointAgent: Endpoint definitions, connection types (database/REST/file), reachability, endpoint-level access/users/groups/tokens configuration details.
            3. DataLinqQueryAgent: Query authoring or interpretation, native query language syntax (SQL/Postgres/etc.), filtering, retrieving data from databases/files/REST, result shape, query-related data concerns.
            4. DataLinqCodeAgent: View layer or UI code: HTML, CSS, vanilla JS, basic C# Razor (if/for/foreach, simple LINQ), DataLinqHelpers usage, rendering logic, layout/composition.

            Multi-Topic Handling:
            - If the latest user intent spans multiple domains, include all relevant agents.
            - For mixed conceptual + specific code: include both General and the specific (Query/Code/Endpoint) agent.
            - If uncertainty between two domains, include both.

            Exclusions / Fallback:
            - If no domain can be inferred, return only DataLinqGeneralAgent.
            - Do NOT invent participant names.

            Ranking (most relevant first):
            Priority by direct match strength to the most specific part of the latest user request. If multiple, order: Endpoint specificity > Query syntax/data retrieval > Code/View rendering > General conceptual. If the request is purely conceptual, General goes first (alone). If conceptual plus a specific layer, put the specific layer first, then General.

            Output Format (strict):
            Return ONLY a single line with the selected participant names separated by commas, no spaces. Example: DataLinqQueryAgent,DataLinqCodeAgent
            If only one: DataLinqGeneralAgent

            History:
            {jsonContent}
            """);

            previousAgents.AddRange(result.ToString()?.Trim().Split(','));
        }

        var nextAgent = previousAgents.First();
        previousAgents.Remove(nextAgent);
        previousAgents.Add(nextAgent);
        iterationCount++;

        return new GroupChatManagerResult<string>(nextAgent.ToString()?.Trim() ?? string.Empty) { Reason = "Custom selection logic." };
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

        bool shouldEnd = iterationCount != 0 && iterationCount >= previousAgents.Count;
        return ValueTask.FromResult(new GroupChatManagerResult<bool>(shouldEnd) { Reason = "Custom termination logic." });
    }
}