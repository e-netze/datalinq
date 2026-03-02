using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace E.DataLinq.Core.Services.Localization
{
    public static class LocalizationServiceExtensions
    {
        public static IServiceCollection AddMarkdownLocalization(this IServiceCollection services,string language,string resourceFolder = "Localization")
        {
            var basePath = AppContext.BaseDirectory;
            var resourcePath = Path.Combine(basePath, resourceFolder);

            if (!Directory.Exists(resourcePath))
                throw new DirectoryNotFoundException(
                    $"Localization folder not found at: {resourcePath}");

            var provider = new MarkdownLocalizationProvider(resourcePath);
            var localizer = provider.GetLocalizer(language);

            services.AddSingleton(provider);
            services.AddSingleton(localizer);

            return services;
        }
    }
}