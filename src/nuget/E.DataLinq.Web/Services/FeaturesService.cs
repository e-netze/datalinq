using E.DataLinq.Core.Models;
using E.DataLinq.Core.Services.Persistance;
using E.DataLinq.Web.Models;
using E.DataLinq.Web.Services.VersionControl;
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
    private readonly IServiceProvider _serviceProvider;

    public FeaturesService(
        IOptions<DataLinqVersionControlOptions> versionControlOptions,
        IOptions<DataLinqCodeApiOptions> codeOptions,
        IServiceProvider serviceProvider
        )
    {
        _versionControlOptions = versionControlOptions.Value;
        _codeOptions = codeOptions.Value;
        _serviceProvider = serviceProvider;
    }

    public FeaturesResult GetFeatures()
    {
        return new FeaturesResult
        {
            VersionControl = _versionControlOptions.IsConfigured,
            Sandbox = _codeOptions.InitializeSandboxOnStartup,
            Mcp = E.DataLinq.Web.Mcp.DataLinqMcpExtensions.IsEnabled(_serviceProvider)
        };
    }
}
