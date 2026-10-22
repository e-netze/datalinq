using E.DataLinq.Web.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;
using System;

namespace E.DataLinq.Web.Mcp;

static public class DataLinqMcpExtensions
{
    // Registers the MCP server only for Test/Development environments.
    static public IServiceCollection AddDataLinqMcp(this IServiceCollection services,
                                                    E.DataLinq.Core.DataLinqEnvironmentType environmentType,
                                                    Action<DataLinqMcpOptions> configure = null)
    {
        if (!DataLinqMcpEnvironment.IsMcpAllowed(environmentType))
        {
            return services;
        }

        services.Configure<DataLinqMcpOptions>(options =>
        {
            configure?.Invoke(options);
        });
        services.AddHttpContextAccessor();

        services.AddSingleton<DataLinqMcpTokenStore>();
        services.AddSingleton<DataLinqMcpRegistrationMarker>();
        services.AddSingleton<DataLinqMcpApproval>();
        services.AddTransient<DataLinqMcpToolContext>();

        services.AddMcpServer(options =>
                {
                    options.ServerInfo = new ModelContextProtocol.Protocol.Implementation()
                    {
                        Name = "datalinq",
                        Version = typeof(DataLinqMcpExtensions).Assembly.GetName().Version?.ToString() ?? "1.0"
                    };
                    options.ServerInstructions =
                        "DataLinq MCP server for authoring DataLinq queries, views, dashboards and PDF reports (Test/Development only). " +
                        "Endpoints are read-only ids (connection details are never exposed); there are no Git operations and no delete tools. " +
                        "Explore schemas and test statements with run_sql_preview before create_query/update_query (syntax: get_query_reference). " +
                        "Always verify_view before saving and inspect the result via get_preview_link in a browser before presenting it. " +
                        "Query runs, writes and preview links are approved by the user in a server-side dialog; if declined or refused, do not retry - ask the user. " +
                        "Never embed images (base64/data:/inline SVG) or imitate logos with CSS; use DLH.GetImage, DLH.GetAgsImage, SECURITY.GetAgsImage " +
                        "or a same-sized placeholder and ask the user for the image URL.";
                })
                .WithHttpTransport(options =>
                {
                    // Sessions for clients that need them (approval dialogs), stateless requests for all others.
                    options.SessionMode = HttpServerSessionMode.StatefulForInitializeClients;
                })
                .WithTools<DataLinqMcpSharedTools>()
                .WithTools<DataLinqMcpQueryTools>()
                .WithTools<DataLinqMcpPdfTools>()
                .WithTools<DataLinqMcpDashboardTools>();

        return services;
    }

    // Adds the middleware (call after UseDatalinqTokenAuthorization). No-op outside Test/Development.
    static public IApplicationBuilder UseDataLinqMcp(this IApplicationBuilder app)
    {
        if (!IsEnabled(app.ApplicationServices))
        {
            return app;
        }

        app.UseMiddleware<DataLinqMcpBearerMiddleware>();
        app.UseMiddleware<DataLinqMcpPreview>();

        return app;
    }

    // Maps the MCP endpoint. No-op outside Test/Development.
    // Agent tokens are generated in DataLinq Code (DataLinqCodeApi/mcp/token).
    static public IEndpointRouteBuilder MapDataLinqMcp(this IEndpointRouteBuilder endpoints)
    {
        if (!IsEnabled(endpoints.ServiceProvider))
        {
            return endpoints;
        }

        endpoints.MapMcp(DataLinqMcpOptions.RoutePrefix);

        return endpoints;
    }

    static public bool IsEnabled(IServiceProvider serviceProvider)
    {
        var environment = serviceProvider.GetService<IOptions<DataLinqOptions>>()?.Value?.EnvironmentType
                          ?? E.DataLinq.Core.DataLinqEnvironmentType.Default;

        return serviceProvider.GetService<DataLinqMcpRegistrationMarker>() != null &&
               DataLinqMcpEnvironment.IsMcpAllowed(environment);
    }

    private class DataLinqMcpRegistrationMarker { }
}
