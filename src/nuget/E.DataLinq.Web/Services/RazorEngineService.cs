using E.DataLinq.Core.Exceptions;
using E.DataLinq.Core.Models;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Services.Abstraction;
using Microsoft.Extensions.Options;
using RazorEngine;
using RazorEngine.Configuration;
using RazorEngine.Templating;
using RazorEngine.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services;

public class RazorEngineService : IRazorCompileEngineService
{
    static bool _intialized = false;

    public readonly DataLinqOptions _options;

    public RazorEngineService(IOptionsMonitor<DataLinqOptions> optionsMonitor)
    {
        _options = optionsMonitor.CurrentValue;

        if (!_intialized)
        {
            _intialized = true;

            var config = new TemplateServiceConfiguration();
            // .. configure your instance
            config.Debug = false;
            config.DisableTempFileLocking = true;

            config.CachingProvider = new DefaultCachingProvider(t =>
            {
                try
                {
                    var di = new DirectoryInfo(t);
                    di.Delete(true);

                    Console.WriteLine($"Razor: Deleted temp directory {t}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Razor: Error deleting temp directory ({t}): {ex.Message}");
                }
            });

            var service = RazorEngine.Templating.RazorEngineService.Create(config);
            Engine.Razor = service;
        }
    }

    public string EngineId => RazorEngineIds.LegacyEngine;

    public object RawString(string str)
    {
        return new RawString(str);
    }

    public Task<string> RunCompile<TModel>(string code, string razorCacheId, TModel model = null)
        where TModel : class
    {
        try
        {
            if (model == null)
            {
                //var config = new TemplateServiceConfiguration();
                //config.Debug = false;
                //config.DisableTempFileLocking = true;

                //var service = RazorEngine.Templating.RazorEngineService.Create(config);

                //service.Compile(code, razorCacheId, modelType);
                //return String.Empty;

                Engine.Razor.Compile(code, razorCacheId, typeof(TModel));

                if (_options.RunGarbageCollectAfterCompile)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }

                return Task.FromResult(String.Empty);
            }
            else
            {
                if (!Engine.Razor.IsTemplateCached(razorCacheId, typeof(TModel)))
                {
                    Engine.Razor.Compile(code, razorCacheId, typeof(TModel));

                    if (_options.RunGarbageCollectAfterCompile)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                    }

                    var key = Engine.Razor.GetKey(razorCacheId);
                }

                return Task.FromResult(Engine.Razor.Run(razorCacheId, typeof(TModel), model));
            }
        }
        catch (TemplateCompilationException ex)
        {
            var errors = new List<RazorCompileError>();

            if (ex.CompilerErrors != null)
            {
                foreach (var error in ex.CompilerErrors)
                {
                    string codeLine = ex.SourceCode.GetLine(error.Line + 1);  // +1 ... first line ist Comment?

                    errors.Add(new RazorCompileError()
                    {
                        IsWarning = error.IsWarning,
                        ErrorText = error.ErrorText/*+" Stacktrace"+ex.StackTrace*/,
                        Line = error.Line,
                        Column = error.Column,
                        CodeLine = codeLine
                    });
                }
            }
            throw new RazorCompileException()
            {
                CompilerErrors = errors
            };
        }
    }

    public bool IsCompilationCached(string razorCacheId, Type modelType)
    {
        return Engine.Razor.IsTemplateCached(razorCacheId, modelType);
    }
}
