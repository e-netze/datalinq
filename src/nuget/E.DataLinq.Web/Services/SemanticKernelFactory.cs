using E.DataLinq.Web.Services.Abstraction;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services;

public class SemanticKernelFactory : ISemanticKernelFactory
{
    private readonly AiServiceOptions _options;

    public SemanticKernelFactory(IOptions<AiServiceOptions> options)
    {
        _options = options.Value;
    }

    public Kernel CreateKernel()
    {
        IKernelBuilder kernelBuilder = Kernel.CreateBuilder();

        if (_options.UseAzure)
        {
            kernelBuilder.AddAzureOpenAIChatCompletion(
                _options.AzureOpenAi.DeploymentName,
                _options.AzureOpenAi.Endpoint,
                _options.AzureOpenAi.ApiKey
            );
        }
        else
        {
            kernelBuilder.AddOpenAIChatCompletion(
                    _options.OpenAi.ModelId,
                    _options.OpenAi.ApiKey,
                    _options.OpenAi.ServiceUrl
                );
        }

        return kernelBuilder.Build();
    }
}