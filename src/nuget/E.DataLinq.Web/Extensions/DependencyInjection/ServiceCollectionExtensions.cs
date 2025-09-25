#nullable enable

using E.DataLinq.Core.Engines;
using E.DataLinq.Core.Engines.Abstraction;
using E.DataLinq.Core.Models;
using E.DataLinq.Core.Security.Token;
using E.DataLinq.Core.Services;
using E.DataLinq.Core.Services.Abstraction;
using E.DataLinq.Core.Services.Crypto;
using E.DataLinq.Core.Services.Crypto.Abstraction;
using E.DataLinq.Core.Services.Persistance;
using E.DataLinq.Core.Services.Persistance.Abstraction;
using E.DataLinq.Web.Razor;
using E.DataLinq.Web.Services;
using E.DataLinq.Web.Services.Abstraction;
using E.DataLinq.Web.Services.Cache;
using E.DataLinq.Web.Services.Plugins;
using E.DataLinq.Web.Services.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace E.DataLinq.Web.Extensions.DependencyInjection;

static public class ServiceCollectionExtensions
{
    #region DataLinq

    static public IServiceCollection AddDataLinqServices<TPersistProvider,
                                                         TCryptoService>(this IServiceCollection services,
                                                                         Action<DataLinqOptions> dataLinqOptions,
                                                                         Action<PersistanceProviderServiceOptions> persistanceOptions,
                                                                         Action<CryptoServiceOptions> cryptoOptions)
        where TPersistProvider : class, IPersistanceProviderService
        where TCryptoService : class, ICryptoService
    {
        return services.AddDataLinqServices<TPersistProvider, TCryptoService, DefaultHostUrlHelper>(dataLinqOptions, persistanceOptions, cryptoOptions);
    }

    static public IServiceCollection AddDataLinqServices<TPersistProvider,
                                                         TCryptoService,
                                                         TUrlHelperType>(this IServiceCollection services,
                                                                         Action<DataLinqOptions> dataLinqOptions,
                                                                         Action<PersistanceProviderServiceOptions> persistanceOptions,
                                                                         Action<CryptoServiceOptions> cryptoOptions)
        where TPersistProvider : class, IPersistanceProviderService
        where TCryptoService : class, ICryptoService
        where TUrlHelperType : class, IHostUrlHelper
    {
        services.AddHttpContextAccessor();

        return services.Configure(dataLinqOptions)
                       .Configure(persistanceOptions)
                       .Configure(cryptoOptions)
                       .AddSingleton<DataLinqEndpointTypeService>()     // Alwas singleton
                       .AddTransient<IHostUrlHelper, TUrlHelperType>()
                       .AddTransient<IPersistanceProviderService, TPersistProvider>()
                       .AddTransient<ICryptoService, TCryptoService>()
                       .AddTransient<AccessTokenService>()
                       // DatalinqService muss Transient sein!!
                       // IEnumerable<IDataLinqEngine> wird injectet
                       // da diese Transient sind muss auch DataLinqServer Transient sein
                       // Sonst kommt es zu Fehler (IFeatureCollection has been disposed), weil IHttpContextAccessor sonst auch Singleton wird!!!
                       .AddTransient<DataLinqService>()
                       .AddTransient<DataLinqCompilerService>()
                       .AddTransient<AccessControlService>()
                       .AddSingleton<IMonacoSnippetService>(provider => new MonacoSnippetService(typeof(DataLinqHelper)))
                       .AddTransient<IDataLinqEnvironmentService, DataLinqEnvironmentService>()
                       .AddTransient<IRazorCompileEngineService, RazorEngineService>()  // classic version
                       .AddTransient<IRazorCompileEngineService, RazorEngineLanguageEngineRazorService>()  // Datalinq version
                       .AddTransient<IWorkerService, DeleteRazorEngineTempFilesWorkerService>()
                       .AddTransient<IWorkerService, DeleteDataLinqRazorEngineTempFilesWorkerService>()
                       .AddTransient<DataLinqInfoService>()
                       .AddSingleton<IBinaryCache, BinaryCacheWrapper>()
                       .AddSingletonIfNotExists<IDataLinqAccessProviderService, DataLinqAccessProviderService>()
                       .AddHostedService<TimedHostedBackgroundService>()
                       .AddTransient<DataLinqFunctionsPlugin>()
                       .AddTransient<SemanticKernelService>(); ;
    }



    #region Engines & DbFactories

    static public IServiceCollection AddDataLinqSelectEngine<T>(this IServiceCollection services)
        where T : class, IDataLinqSelectEngine
    {
        return services.AddTransient<IDataLinqSelectEngine, T>();
    }

    static public IServiceCollection AddDataLinqExecuteNonQueryEngine<T>(this IServiceCollection services)
        where T : class, IDataLinqExecuteNonQueryEngine
    {
        return services.AddTransient<IDataLinqExecuteNonQueryEngine, T>();
    }

    static public IServiceCollection AddDataLinqDbFactoryProvider<T>(this IServiceCollection services)
        where T : class, IDbFactoryProviderService
    {
        return services.AddTransient<IDbFactoryProviderService, T>();
    }


    static public IServiceCollection AddDefaultDatalinqEngines(
                this IServiceCollection services,
                IConfigurationSection? enginesConfigSection
        )
    {





        services.AddDataLinqSelectEngine<DatabaseEngine>();
        services.AddDataLinqSelectEngine<PlainTextEngine>();
        services.AddDataLinqSelectEngine<DataLinqEngine>();

        #region TextFileEngine

        TextFileEngineOptions options =
            enginesConfigSection?.GetSection("TextFileEngine").Exists() == true
                ? enginesConfigSection.GetSection("TextFileEngine").Get<TextFileEngineOptions>()!
                : TextFileEngineOptions.Default;

        services.Configure<TextFileEngineOptions>(config =>
        {
            config.AllowedPaths = options.AllowedPaths ?? [];
            config.AllowedExtensions = options.AllowedExtensions ?? [];
        });

        services.AddDataLinqSelectEngine<TextFileEngine>();
        services.AddDataLinqSelectEngine<JsonApiEngine>();

        #endregion

        return services;
    }

    #endregion

    static public IServiceCollection AddDataLinqHostAuthenticatoinService<T>(this IServiceCollection services)
        where T : class, IHostAuthenticationService
    {
        return services.AddTransient<IHostAuthenticationService, T>();
    }

    #endregion

    #region DataLinq Code Api

    static public IServiceCollection AddDataLinqCodeApiServices<TIdentityProvider>(this IServiceCollection services,
                                                                                   Action<DataLinqCodeApiOptions> setupAction)
        where TIdentityProvider : class, IDataLinqCodeIdentityProvider
    {
        services.Configure(setupAction);

        return services
            .AddJsLibraryService()
            .AddTransient<IDataLinqCodeIdentityProvider, TIdentityProvider>()
            .AddTransient<IDataLinqCodeIdentityService, DataLinqCodeIdentityService>()
            .AddTransient<IDataLinqAccessTokenAuthProvider, DataLinqAuthTokenHttpHeaderProvider>()
            .AddTransient<IDataLinqAccessTokenAuthProvider, DataLinqAuthTokenCookieProvider>()
            .AddHostedService<SandboxInitializer>();
    }

    #endregion

    static private IServiceCollection AddJsLibraryService(this IServiceCollection services)
    {
        services.Configure<JsLibrariesServiceOptions>(config =>
        {
            config.JsLibibraries = new JsLibrary[]
            {
                new()
                {
                    Name = JsLibNames.Legacy_ChartJs,
                    Description = "(Legacy) ChartJs 2.9.4 use this this DLH.Chart"
                },
                new()
                {
                    Name = JsLibNames.Legacy_ChartJs_Plugin_DataLabels,
                    Description = " (Legacy) optional ChartJS plugin - DataLabels v1.0.0"
                },
                new()
                {
                    Name = JsLibNames.Legacy_ChartJs_Plugin_ColorSchemes,
                    Description = " (Legacy) optional ChartJS plugin - ColorSchemes v0.4.0"
                },

                new()
                {
                    Name = JsLibNames.ChartJs_3x,
                    Description = "ChartJs 3.9.1"
                },
                new()
                {
                    Name = JsLibNames.ChartJs_3x_Plugin_DataLabels,
                    Description = " optional ChartJS plugin - DataLabels v2.2.0"
                },
                new()
                {
                    Name=JsLibNames.D3_7x,
                    Description = "D3 Charting Library v7.9.0"
                },
                new()
                {
                    Name=JsLibNames.JsonEditor,
                    Description = "JsonEditor v10.2.0"
                }
            };
        });

        return services.AddTransient<JsLibrariesService>();
    }

    private static IServiceCollection AddSingletonIfNotExists<TService, TImplementation>(this IServiceCollection services)
                    where TService : class
                    where TImplementation : class, TService
    {
        if (!services.Any(serviceDescriptor => serviceDescriptor.ServiceType == typeof(TService)))
        {
            services.AddSingleton<TService, TImplementation>();
        }

        return services;
    }
}
