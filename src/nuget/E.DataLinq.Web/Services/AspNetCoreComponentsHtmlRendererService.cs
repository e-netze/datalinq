//#nullable enable

//using E.DataLinq.Web.Services.Abstraction;
//using Microsoft.AspNetCore.Components.Web;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Logging;
//using RazorEngine.Text;
//using System;
//using System.Collections.Generic;
//using System.Threading.Tasks;

//namespace E.DataLinq.Web.Services;

//public class AspNetCoreComponentsHtmlRendererService : IRazorCompileEngineService
//{
//    private readonly IServiceProvider _serviceProvider;
//    private readonly ILoggerFactory _loggerFactory;

//    public AspNetCoreComponentsHtmlRendererService(
//            IServiceProvider serviceProvider,
//            ILoggerFactory loggerFactory
//        )
//    {
//        _serviceProvider = serviceProvider;
//        _loggerFactory = loggerFactory;
//    }

//    public bool IsCompilationCached(string razorCacheId, Type modelType)
//    {
//        return false;
//    }

//    public object RawString(string str)
//    {
//        return str;
//    }

//    async public Task<string> RunCompile(string code, string razorCacheId, Type modelType, object? model = null)
//    {
//        await using var htmlRenderer = new HtmlRenderer(_serviceProvider, _loggerFactory);

//        var html = await htmlRenderer.Dispatcher.InvokeAsync(async () =>
//        {
//            var dictionary = new Dictionary<string, object?>
//            {
//                { "Message", "Hello from the Render Message component!" }
//            };

//            var parameters = ParameterView.FromDictionary(dictionary);
//            var output = await htmlRenderer.RenderComponentAsync<RenderMessage>(parameters);

//            return output.ToHtmlString();
//        });
//    }
//}
