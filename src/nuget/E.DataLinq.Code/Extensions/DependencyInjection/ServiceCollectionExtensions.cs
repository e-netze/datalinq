using E.DataLinq.Code.Services;
using E.DataLinq.Core.Services;
using E.DataLinq.Core.Services.Abstraction;
using E.DataLinq.Core.Services.Crypto;
using E.DataLinq.Core.Services.Crypto.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace E.DataLinq.Code.Extensions.DependencyInjection;

static public class ServiceCollectionExtensions
{
    static public IServiceCollection AddDataLinqCodeService(this IServiceCollection services,
                                                            Action<DataLinqCodeOptions> configAction,
                                                            Action<CryptoServiceOptions> cryptoOptions = null)
    {
        if (!services.Any(x => typeof(IHostUrlHelper).IsAssignableFrom(x.ServiceType)))
        {
            services.AddHttpContextAccessor();
            services.AddTransient<IHostUrlHelper, DefaultHostUrlHelper>();
        }

        if (!services.Any(x => typeof(ICryptoService).IsAssignableFrom(x.ServiceType)))
        {
            services.Configure(cryptoOptions);
            services.AddTransient<ICryptoService, CryptoService>();
        }

        if (!services.Any(x => typeof(IDataLinqAccessTreeService).IsAssignableFrom(x.ServiceType)))
        {
            services.AddTransient<IDataLinqAccessTreeService, DataLinqAccessTreeService>();
        }

        return services.Configure(configAction)
                   .AddTransient<DataLinqCodeService>()
                   .AddTransient<DataLinqCodeIndentityService>();
    }

    //static public IServiceCollection AddDataLinqCodeService<TCryptoService,
    //                                                        TUrlHelperType>(this IServiceCollection services,
    //                                                                        Action<DataLinqCodeOptions> configAction,
    //                                                                        Action<CryptoServiceOptions> cryptoOptions)
    //    where TCryptoService : class, ICryptoService
    //    where TUrlHelperType : class, IHostUrlHelper
    //{
    //    services.AddHttpContextAccessor();

    //    return services.Configure(configAction)
    //                   .Configure(cryptoOptions)
    //                   .AddTransient<IHostUrlHelper, TUrlHelperType>()
    //                   .AddTransient<DataLinqCodeService>()
    //                   .AddTransient<DataLinqCodeCookieService>();
    //}
}
