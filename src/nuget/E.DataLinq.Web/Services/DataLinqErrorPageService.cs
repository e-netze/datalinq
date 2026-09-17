using E.DataLinq.Core.Services.Persistance.Abstraction;
using E.DataLinq.Web.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services;

public class DataLinqErrorPageService
{
    public const string GlobalErrorPageName = DataLinqErrorPage.GlobalName;

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

    async public Task<string> GetErrorPageCodeAsync(string name = GlobalErrorPageName)
    {
        try
        {
            var code = await _persistanceProvider.GetErrorPage(NormalizeName(name));

            return String.IsNullOrWhiteSpace(code)
                ? DataLinqErrorPageDefaults.DefaultRazorCode
                : code;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load custom error page {name}", name);

            return DataLinqErrorPageDefaults.DefaultRazorCode;
        }
    }

    public Task<IEnumerable<string>> GetErrorPageNamesAsync()
        => _persistanceProvider.GetErrorPageNames();

    public Task<bool> StoreErrorPageCodeAsync(string name, string razorCode)
        => _persistanceProvider.StoreErrorPage(NormalizeName(name), razorCode ?? String.Empty);

    public Task<bool> StoreErrorPageCodeAsync(string razorCode)
        => StoreErrorPageCodeAsync(GlobalErrorPageName, razorCode);

    public Task<bool> DeleteErrorPageAsync(string name)
    {
        if (IsGlobal(name))
        {
            throw new Exception("The global error page can not be deleted");
        }

        return _persistanceProvider.DeleteErrorPage(NormalizeName(name));
    }

    public Task ValidateErrorPageCodeAsync(string razorCode)
        => _compiler.ValidateErrorPageCode(razorCode);

    async public Task<string> RenderAsync(Exception exception,
                                          DataLinqErrorPageModel model,
                                          string errorPageName = null)
    {
        model ??= new DataLinqErrorPageModel(exception);
        model.IsDevelopment = _environment.IsDevelopment();

        string customCode = await LoadCodeOrFallbackAsync(errorPageName);

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

    async private Task<string> LoadCodeOrFallbackAsync(string errorPageName)
    {
        // a view can reference a named error page => if it is gone, fall back to the global one
        if (!String.IsNullOrWhiteSpace(errorPageName) && !IsGlobal(errorPageName))
        {
            try
            {
                var namedCode = await _persistanceProvider.GetErrorPage(NormalizeName(errorPageName));

                if (!String.IsNullOrWhiteSpace(namedCode))
                {
                    return namedCode;
                }

                _logger.LogWarning("Error page {name} does not exist, falling back to the global error page", errorPageName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load error page {name}, falling back to the global error page", errorPageName);
            }
        }

        try
        {
            return await _persistanceProvider.GetErrorPage(GlobalErrorPageName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load global error page, using default");

            return null;
        }
    }

    static private bool IsGlobal(string name)
        => String.IsNullOrWhiteSpace(name) ||
           GlobalErrorPageName.Equals(name, StringComparison.OrdinalIgnoreCase);

    static private string NormalizeName(string name)
        => String.IsNullOrWhiteSpace(name)
            ? GlobalErrorPageName
            : name.Trim();

    private string MinimalErrorHtml(Exception exception)
        => $"<div id='datalinq-services-error' style='background-color:#efefaa;display:inline-block;margin:5px;padding:10px;border:1px solid red'>{System.Web.HttpUtility.HtmlEncode(exception?.Message)}</div>";
}
