using DataLinq.Api.Extensions;
using DataLinq.Api.Services;
using E.DataLinq.Core.Services.Crypto;
using E.DataLinq.Core.Services.Persistance;
using E.DataLinq.Web;
using E.DataLinq.Web.Extensions.DependencyInjection;
using E.DataLinq.Web.Mcp;
using E.DataLinq.Web.Services;
using E.DataLinq.Web.Services.Abstraction;
using E.DataLinq.Web.Services.TokenCache;
using E.DataLinq.Web.Services.VersionControl;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(Path.Combine("_config", "datalinq.api.json"), optional: false, reloadOnChange: false);
builder.Configuration.AddUserSecrets<Program>();
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

builder.AddServiceDefaults();
builder.Services.AddControllersWithViews();

#region E.DataLinq.Web Services

var dataLinqEnvironment = builder.Configuration.GetValue("DataLinq.Api:EnvironmentType", E.DataLinq.Core.DataLinqEnvironmentType.Development);

builder.Services.AddDataLinqServices<FileSystemPersistanceService, CryptoService>(
    dataLinqOptions: options =>
    {
        options.EnvironmentType = dataLinqEnvironment;
        options.EngineId = builder.Configuration["DataLinq.Api:Razor:Engine"]?.ToLowerInvariant() switch
        {
            "legacy" => RazorEngineIds.LegacyEngine,
            _ => RazorEngineIds.DataLinqLanguageEngineRazor,
        };
        options.TempPath = Path.Combine(Path.GetTempPath(), "datalinq");
        options.AddToImageRequestWhiteList(
            builder.Configuration.GetSection("DataLinq.Api:ImageRequestWhiteList").Get<string[]>()
            ?? Array.Empty<string>());
    },
    persistanceOptions: options =>
    {
        options.ConnectionString = builder.Configuration["DataLinq.Api:StoragePath"];
        if (
            Enum.TryParse<EncryptionLevel>(
                builder.Configuration["DataLinq.Api:Crypto:SecureStringEncryptionLevel"],
                out EncryptionLevel encryptionLevel
            )
        )
        {
            options.SecureStringEncryptionLevel = encryptionLevel;
        }
    },
    cryptoOptions: options =>
    {
        options.DefaultPassword = builder.Configuration["DataLinq.Api:Crypto:DefaultPasswort"].OrRandomPassword();
        options.HashBytesSalt = Convert.FromBase64String(
            builder.Configuration["DataLinq.Api:Crypto:SaltBytes"].OrRandomSaltBase64()
        );
    }
);

var tokenStoreConfigSection = builder.Configuration.GetSection(DataLinqTokenStoreOptions.Key);
builder.Services.Configure<DataLinqTokenStoreOptions>(tokenStoreConfigSection);
builder.Services.AddDataLinqCacheTokenStore(tokenStoreConfigSection);

builder.Services.AddDefaultDatalinqEngines(builder.Configuration.GetSection("DataLinq.Api:SelectEngines"));
builder.Services.AddDataLinqDbFactoryProvider<E.DataLinq.Engine.Postgres.DbFactoryProvider>();
builder.Services.AddDataLinqDbFactoryProvider<E.DataLinq.Engine.MsSqlServer.MsSqlClientDbFactoryProvider>();
builder.Services.AddDataLinqDbFactoryProvider<E.DataLinq.Engine.SQLite.DbFactoryProvider>();
builder.Services.AddDataLinqDbFactoryProvider<E.DataLinq.Engine.OracleClient.DbFactoryProvider>();

builder.Services.AddDataLinqVersionControlServices(options =>
    builder.Configuration.GetSection(DataLinqVersionControlOptions.Key).Bind(options));

#endregion

#region E.DataLinq.Web Code.Api Services

if (builder.Configuration.GetSection("DataLinq.CodeApi").Exists())
{
    builder.Services.AddDataLinqCodeApiServices<DataLinqCodeIdentityProvider>(config =>
    {
        config.AccessTokenIssuer = builder.Configuration["DataLinq.CodeApi:AccessTokenIssuer"];
        config.DataLinqCodeClients = builder
            .Configuration.GetSection("DataLinq.CodeApi:ClientEndpoints")
            .Get<string[]>();
        config.InitializeSandboxOnStartup = builder.Configuration
            .GetValue<bool>("DataLinq.CodeApi:InitializeSandboxOnStartup", true);
    });
}

#endregion

// MCP server for agents (PDF report authoring) - only registered in Test/Development
builder.Services.AddDataLinqMcp(dataLinqEnvironment, options =>
{
    // default: <persistance storage path>/mcp-agent-tokens.blb (encrypted like the secrets)
    options.AgentTokenRegistryPath = builder.Configuration["DataLinq.Api:Mcp:AgentTokenRegistryPath"];
    options.MaxAgentTokenLifetimeHours = builder.Configuration.GetValue("DataLinq.Api:Mcp:MaxAgentTokenLifetimeHours", 24 * 7);
});

builder.Services.AddScoped<IRoutingEndPointReflectionProvider, RoutingEndPointReflectionService>();

#if !DEBUG
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;

    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
#endif

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

#if !DEBUG
var pathBase = builder.Configuration["ASPNETCORE_PATHBASE"];
if (!string.IsNullOrWhiteSpace(pathBase))
{
    app.UsePathBase(pathBase);
}
#endif

app.UseRouting();

app.UseAuthorization();
app.UseDatalinqTokenAuthorization();
app.UseDataLinqMcp();

app.MapStaticAssets();

app.AddDataLinqEndpoints();
app.MapDataLinqMcp();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();