using E.DataLinq.Core;
using System;

namespace E.DataLinq.Web.Mcp;

public class DataLinqMcpOptions
{
    public const string RoutePrefix = "/datalinq-mcp";

    // Upper limit for agent tokens generated in DataLinq Code (Settings > MCP Agent Token).
    public int MaxAgentTokenLifetimeHours { get; set; } = 24 * 7;
    // Preview link (URL token): single use, only needs to live until the agent opens it.
    public int PreviewLinkLifetimeSeconds { get; set; } = 120;
    // Browser session (HttpOnly cookie) created when the preview link is opened.
    public int PreviewSessionLifetimeSeconds { get; set; } = 600;

    // Encrypted json file listing issued (non-revoked) agent tokens (hash, name, user, expiration).
    // Tokens not in this list are rejected. Default: <persistance storage path>/mcp-agent-tokens.blb
    public string AgentTokenRegistryPath { get; set; }
}

static public class DataLinqMcpEnvironment
{
    static public bool IsMcpAllowed(DataLinqEnvironmentType environmentType)
        => environmentType == DataLinqEnvironmentType.Test ||
           environmentType == DataLinqEnvironmentType.Development;

    static public void EnsureMcpAllowed(DataLinqEnvironmentType environmentType)
    {
        if (!IsMcpAllowed(environmentType))
        {
            throw new InvalidOperationException($"DataLinq MCP is only available in Test or Development environments (current: {environmentType}).");
        }
    }
}
