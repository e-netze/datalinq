using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace E.DataLinq.Core.Models;

public class SuccessModel
{
    public SuccessModel() => this.Success = true;

    public SuccessModel(bool result)
    {
        this.Success = result;
        if (!Success)
        {
            this.ErrorMessage = "An unknown error occurred.";
        }
    }

    public SuccessModel(Exception ex)
    {
        this.Success = false;
        this.ErrorMessage = ex.Message;
    }

    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("error_message", NullValueHandling = NullValueHandling.Ignore)]
    public string ErrorMessage { get; set; }

    [JsonProperty("compiler_errors", NullValueHandling = NullValueHandling.Ignore)]
    public IEnumerable<RazorCompileError> CompilerErrors { get; set; }
}
