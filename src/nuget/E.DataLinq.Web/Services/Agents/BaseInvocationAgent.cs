using E.DataLinq.Web.Services.Abstraction;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services.Agents;

public abstract class BaseInvocationAgent<TInput, TOutput> : IAgent<TInput, TOutput>
{
    protected readonly ISemanticKernelFactory KernelFactory;
    protected readonly string PromptPath;

    protected BaseInvocationAgent(ISemanticKernelFactory kernelFactory, string promptPath)
    {
        KernelFactory = kernelFactory;
        PromptPath = promptPath;
    }

    public async Task<TOutput> RunAsync(TInput input)
    {
        var kernel = KernelFactory.CreateKernel();
        var prompt = await File.ReadAllTextAsync(PromptPath);

        var parameters = BuildParameters(input);
        var result = await kernel.InvokePromptAsync(prompt, parameters);
        var text = result.ToString()?.Trim() ?? string.Empty;

        return ProcessResult(text, input);
    }

    protected abstract KernelArguments BuildParameters(TInput input);
    protected abstract TOutput ProcessResult(string result, TInput input);
}
