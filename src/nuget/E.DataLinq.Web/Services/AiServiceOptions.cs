using E.DataLinq.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services;

public class AiServiceOptions
{
    public static readonly string Key = "AiService";
    public bool UseAzure { get; set; } = true;
    public AzureOpenAiOptions AzureOpenAi { get; set; } = new();
    public OpenAiOptions OpenAi { get; set; } = new();
}

public class AzureOpenAiOptions
{
    public string DeploymentName { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

public class OpenAiOptions
{
    public string ModelId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ServiceUrl { get; set; }
}
