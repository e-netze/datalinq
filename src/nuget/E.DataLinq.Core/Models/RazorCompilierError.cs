using Newtonsoft.Json;

namespace E.DataLinq.Core.Models;

public class RazorCompileError
{
    [JsonProperty("is_warning")]
    public bool IsWarning { get; set; }

    [JsonProperty("error_text")]
    public string ErrorText { get; set; }

    [JsonProperty("line")]
    public int Line { get; set; }

    [JsonProperty("column")]
    public int Column { get; set; }

    [JsonProperty("code_line")]
    public string CodeLine { get; set; }
}
