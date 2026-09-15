using E.DataLinq.Core.Services.Persistance.Abstraction;
using E.DataLinq.Web.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services;

public class DataLinqErrorPageService
{
    private readonly IPersistanceProviderService _persistanceProvider;
    private readonly DataLinqCompilerService _compiler;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DataLinqErrorPageService> _logger;

    public DataLinqErrorPageService(IPersistanceProviderService persistanceProvider,
                                    DataLinqCompilerService compiler,
                                    IWebHostEnvironment environment,
                                    ILogger<DataLinqErrorPageService> logger)
    {
        _persistanceProvider = persistanceProvider;
        _compiler = compiler;
        _environment = environment;
        _logger = logger;
    }

    async public Task<string> GetErrorPageCodeAsync()
    {
        try
        {
            var code = await _persistanceProvider.GetErrorPage();

            return String.IsNullOrWhiteSpace(code)
                ? DataLinqErrorPageDefaults.DefaultRazorCode
                : code;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load custom error page");

            return DataLinqErrorPageDefaults.DefaultRazorCode;
        }
    }

    public Task<bool> StoreErrorPageCodeAsync(string razorCode)
        => _persistanceProvider.StoreErrorPage(razorCode ?? String.Empty);

    public Task ValidateErrorPageCodeAsync(string razorCode)
        => _compiler.ValidateErrorPageCode(razorCode);

    async public Task<string> RenderAsync(Exception exception, DataLinqErrorPageModel model)
    {
        model ??= new DataLinqErrorPageModel(exception);
        model.IsDevelopment = _environment.IsDevelopment();

        string customCode = null;

        try
        {
            customCode = await _persistanceProvider.GetErrorPage();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load custom error page, using default");
        }

        if (!String.IsNullOrWhiteSpace(customCode))
        {
            try
            {
                return await _compiler.RenderErrorPage(customCode, model);
            }
            catch (Exception ex)
            {
                // the custom error page itself is broken => never bubble up, fall back to default
                _logger.LogError(ex, "Custom error page could not be rendered, using default");
            }
        }

        try
        {
            return await _compiler.RenderErrorPage(DataLinqErrorPageDefaults.DefaultRazorCode, model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Default error page could not be rendered");

            return MinimalErrorHtml(exception);
        }
    }

    private string MinimalErrorHtml(Exception exception)
        => $"<div id='datalinq-services-error' style='background-color:#efefaa;display:inline-block;margin:5px;padding:10px;border:1px solid red'>{System.Web.HttpUtility.HtmlEncode(exception?.Message)}</div>";
}
