using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services;

public class AgentOptions
{
    public static readonly string Key = "Agent";
    public string DataLinqOrchestrator { get; set; } = string.Empty;
    public string DataLinqQuery { get; set; } = string.Empty;
    public string DataLinqCode { get; set; } = string.Empty;
    public string DataLinqEndpoint { get; set; } = string.Empty;
    public string UserHistorySummarizerAgent { get; set; } = string.Empty;
}
