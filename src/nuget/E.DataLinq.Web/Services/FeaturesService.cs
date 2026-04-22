using E.DataLinq.Core.Models;
using E.DataLinq.Core.Services.Persistance;
using E.DataLinq.Web.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace E.DataLinq.Web.Services;

public class FeaturesService
{
    private readonly DataLinqVersionControlOptions _versionControlOptions;
    private readonly DataLinqCodeApiOptions _codeOptions;
    private readonly AiServiceOptions _aiOptions;

    public FeaturesService(
        IOptions<DataLinqVersionControlOptions> versionControlOptions,
        IOptions<DataLinqCodeApiOptions> codeOptions,
        IOptions<AiServiceOptions> aiOptions
        )
    {
        _versionControlOptions = versionControlOptions.Value;
        _codeOptions = codeOptions.Value;
        _aiOptions = aiOptions.Value;
    }

    public FeaturesResult GetFeatures()
    {
        return new FeaturesResult
        {
            VersionControl = _versionControlOptions.UseVersionControl,
            Sandbox = _codeOptions.InitializeSandboxOnStartup,
            Copilot = !string.IsNullOrEmpty(_aiOptions.AzureOpenAi.Endpoint) || !string.IsNullOrEmpty(_aiOptions.OpenAi.ServiceUrl)
        };
    }
}
