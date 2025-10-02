using E.DataLinq.Web.Services.Abstraction;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services.Agents
{
    public class UserHistorySummarizerAgent : BaseInvocationAgent<string[], string>
    {
        public UserHistorySummarizerAgent(ISemanticKernelFactory kernelFactory, IOptions<AgentOptions> options)
            : base(kernelFactory, options.Value.UserHistorySummarizerAgent) { }

        protected override KernelArguments BuildParameters(string[] input)
        {
            var jsonContent = JsonSerializer.Serialize(input, new JsonSerializerOptions { WriteIndented = true });

            return new KernelArguments
            {
                ["lastQuestion"] = input.Last(),
                ["history"] = jsonContent
            };
        }

        protected override string ProcessResult(string result, string[] input)
        {
            return result;
        }
    }
}
