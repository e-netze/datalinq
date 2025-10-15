using E.DataLinq.Core.Services.Persistance.Abstraction;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static E.DataLinq.Web.Services.CopilotReflectionInitializer;

namespace E.DataLinq.Web.Services.Plugins;

public class DataLinqHelperFunctionsPlugin
{
    private List<MethodInfoObject> _dataLinqCopilotHelpers;

    public DataLinqHelperFunctionsPlugin()
    {
        _dataLinqCopilotHelpers = JsonSerializer.Deserialize<List<MethodInfoObject>>(File.ReadAllText("datalinq_helpers_for_copilot.json"));
    }

    public record DataLinqFunctionInfo(string Name, string ShortDescription);

    [KernelFunction("get_all_datalinq_functions")]
    [Description("Gets all of the datalinq function names and short descriptions")]
    public List<DataLinqFunctionInfo> GetAllDataLinqFunctions()
    {
        List<DataLinqFunctionInfo> list = new List<DataLinqFunctionInfo>();
        _dataLinqCopilotHelpers.ForEach(fe => list.Add(new DataLinqFunctionInfo(fe.Name, fe.Description)));
        return list;
    }

    [KernelFunction("get_datalinq_function_details")]
    [Description("Gets the details and usage information of a specific DataLinq function")]
    public MethodInfoObject GetDataLinqFunctionDetails([Description("The function name")] string functionName)
    {
        return _dataLinqCopilotHelpers.Where(w => w.Name.Equals(functionName)).FirstOrDefault();
    }
}
