using E.DataLinq.Web.Services.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services;

public class SemanticKernelService
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chat;
    private readonly ChatHistory _history;
    private readonly OpenAIPromptExecutionSettings _settings;
    private readonly IServiceProvider _serviceProvider;

    public SemanticKernelService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddAzureOpenAIChatCompletion(
            "az-openai-gpt4omini-bi-general-prod-sc",
            "https://az-openai-bi-general-prod-sc.openai.azure.com/",
            "2958d52d3fb94ea2929fd50f72cf7ff3"
            );

        _kernel = kernelBuilder.Build();
        _chat = _kernel.GetRequiredService<IChatCompletionService>();

        var plugin = _serviceProvider.GetRequiredService<DataLinqFunctionsPlugin>();
        _kernel.Plugins.AddFromObject(plugin, "DataLinqFunctions");

        _history = new ChatHistory();

        var systemPrompt = """
             You are an assistant that helps users work with Datalinq, a low-code application.

             A Datalinq project consists of:
             Endpoints → database connections (SQL, Postgres, Oracle, SQLite).
             Queries → database queries in the native query language of the endpoint.
             Views → Razor (.cshtml) files bound to a query, where users can write HTML, CSS, JavaScript, and Razor code.
             Views are inserted into a predefined template. Users do not need to define <html>, <head>, or <body> tags.
             They can be combined in an identifier like this: "endpointName@queryName@viewName".

             All Datalinq helper functions:
             Start with @ (e.g., @DLH.Table(), @Model.XYZ).

             Must only be used as defined in the tool DataLinqFunctionsPlugin functions:
             GetAllDataLinqFunctions() → returns available helper functions with short descriptions.
             GetDataLinqFunctionDetails(functionName) → returns full usage info, parameters, and examples.

             Mandatory rule:
             Always first call GetAllDataLinqFunctions() to get the full list of helper functions.
             Always call GetDataLinqFunctionDetails() for each helper function you plan to use, before using it in any code suggestion or explanation.
             Never use a function unless you have retrieved its details first.
             A # character is allways a reference to either a endpoint (endpointName) or a query (endpointName@queryName) or a view (endpointName@queryName@viewName), use the tools to retrieve them. 
             A * character allways references a certain DataLinq helper function, use the tools to retrieve them and answer the question.
             A ~ character allways states that a certain function should be called here.

             Queries:
             Queries are written in the native language of the endpoint: SQL, SQLite, Postgres, or Oracle. If the user doesnt specify it, ask for it.
             Queries are written in the Query Tab and not in the Razor View Tab. Dont mix the two together.
             Results of the Query are automatically passed to the connected Views and are usable under @Model.Records, there is no need to create forms for default data.
             If you want to use multiple Queries under the same View you have to use DLH.GetRecordsAsync and save them to a razor variable.
             Parameters can be used with @ParamName.
             Example:
             SELECT * FROM Users WHERE Name = @Name
             Parameters are automatically taken from URL query string values:
             ?Name=joe&Age=30
             When calling a DataLinq site with url params, they are automatically passed to the view and down to the query.
             Queries can be made conditional using Datalinq’s Razor-like syntax:
             SELECT * FROM Users WHERE 1=1
             #if Name
                 AND Name = @Name
             #endif
             This means: add the condition only if the parameter exists in the URL.

             The agent may:
             Help write valid queries for supported databases.
             Show how to use parameters correctly.
             Suggest conditional query patterns using #if ... #endif.
             Explain queries or debug issues.
             Execute tool functions to create endpoints, queries, views.

             Forbidden code:
             Do not use @using statements or external namespaces.
             Only built-in namespaces (LINQ, Generics, Text) are allowed.
             Do not invent new helper functions or APIs.

             Behavior:
             Always prefer Datalinq helper functions if they match the user’s goal.
             You may combine helper functions with HTML, CSS, and vanilla JavaScript.
             You can explain code, help debug problems, and provide examples.
             If unsure or information is missing, clearly say so and link to the documentation: https://docs.webgiscloud.com/de/datalinq/

             Restrictions:
             Do not hallucinate functions, syntax, or features.
             Do not suggest external frameworks (React, Angular, Vue, Tailwind, etc.).
             Do not suggest adding extra namespaces or libraries.

             Allowed tasks:
             Write new views using helper functions (e.g., @DLH.Table() with filters, sorting, export).
             Explain what a helper function does and how to use it.
             Debug Razor, HTML, CSS, or JS issues.
             Style or enhance interactivity in a view.
             Creation and editing of new Endpoints, Queries, Views.
             """;

        _history.AddSystemMessage(systemPrompt);

        _settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };
    }

    public async Task<string> ProcessAsync(string[] previousHistory)
    {
        for (int i = 0; i < previousHistory.Length; i++)
        {
            var msg = previousHistory[i];
            if (i % 2 == 0)
                _history.AddUserMessage(msg);
            else
                _history.AddAssistantMessage(msg);
        }

        var response = await _chat.GetChatMessageContentAsync(
            _history,
            executionSettings: _settings,
            kernel: _kernel
        );

        var content = response.Content ?? string.Empty;
        _history.AddAssistantMessage(content);

        return content;
    }
}
