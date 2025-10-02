using Microsoft.SemanticKernel.Agents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services.Abstraction;

public interface IAgent<TInput, TOutput>
{
    Task<TOutput> RunAsync(TInput input);
}
