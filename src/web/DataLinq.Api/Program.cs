using DataLinq.Api.Extensions;
using DataLinq.Api.Services;
using E.DataLinq.Core.Services.Crypto;
using E.DataLinq.Core.Services.Persistance;
using E.DataLinq.Web;
using E.DataLinq.Web.Extensions.DependencyInjection;
using E.DataLinq.Web.Services;
using E.DataLinq.Web.Services.Abstraction;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(Path.Combine("_config", "datalinq.api.json"), optional: false, reloadOnChange: false);

builder.AddServiceDefaults();
builder.Services.AddControllersWithViews();

#region E.DataLinq.Web Services

builder.Services.AddDataLinqServices<FileSystemPersistanceService, CryptoService>(
    dataLinqOptions: options =>
    {
        options.EnvironmentType = E.DataLinq.Core.DataLinqEnvironmentType.Development;
        options.EngineId = builder.Configuration["DataLinq.Api:Razor:Engine"]?.ToLowerInvariant() switch
        {
            "legacy" => RazorEngineIds.LegacyEngine,
            _ => RazorEngineIds.DataLinqLanguageEngineRazor,
        };
        options.TempPath = Path.Combine(Path.GetTempPath(), "datalinq");
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

builder.Services.Configure<AiServiceOptions>(builder.Configuration.GetSection(AiServiceOptions.Key));
builder.Services.AddDefaultDatalinqEngines(builder.Configuration.GetSection("DataLinq.Api:SelectEngines"));
builder.Services.AddDataLinqDbFactoryProvider<E.DataLinq.Engine.Postgres.DbFactoryProvider>();
builder.Services.AddDataLinqDbFactoryProvider<E.DataLinq.Engine.MsSqlServer.MsSqlClientDbFactoryProvider>();
builder.Services.AddDataLinqDbFactoryProvider<E.DataLinq.Engine.SQLite.DbFactoryProvider>();
builder.Services.AddDataLinqDbFactoryProvider<E.DataLinq.Engine.OracleClient.DbFactoryProvider>();

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
        config.StoragePath = builder.Configuration["DataLinq.Api:StoragePath"];
    });
}

#endregion

builder.Services.AddScoped<IRoutingEndPointReflectionProvider, RoutingEndPointReflectionService>();

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
app.UseRouting();

app.UseAuthorization();
app.UseDatalinqTokenAuthorization();

app.MapStaticAssets();

app.AddDataLinqEndpoints();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();