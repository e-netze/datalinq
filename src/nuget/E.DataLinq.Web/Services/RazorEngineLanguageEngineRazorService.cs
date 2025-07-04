using E.DataLinq.Core.Exceptions;
using E.DataLinq.Core.Models;
using E.DataLinq.Core.Services.Abstraction;
using E.DataLinq.LanguageEngine.Razor;
using E.DataLinq.LanguageEngine.Razor.Exceptions;
using E.DataLinq.LanguageEngine.Razor.Templates;
using E.DataLinq.LanguageEngine.Razor.Utilities;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Services.Abstraction;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services;

public class RazorEngineLanguageEngineRazorService : IRazorCompileEngineService
{
    private const string CacheNamespace = "datalinq_razorengine";
    private readonly DataLinqOptions _options;
    private readonly IBinaryCache _binaryCache;

    public RazorEngineLanguageEngineRazorService(
        IBinaryCache binaryCache,
        IOptionsMonitor<DataLinqOptions> optionsMonitor)
    {
        _options = optionsMonitor.CurrentValue;
        _binaryCache = binaryCache;
    }

    public string EngineId => RazorEngineIds.DataLinqLanguageEngineRazor;

    public bool IsCompilationCached(string razorCacheId, Type modelType)
    {
        return _binaryCache.HasData(razorCacheId.ToRazorAssemblyFilename(), CacheNamespace);
    }

    public object RawString(string str)
    {
        return new RawContent(str);
    }

    async public Task<string> RunCompile<TModel>(string code, string razorCacheId, TModel model = null)
        where TModel : class
    {
        try
        {
            var razorEngine = new DataLinqRazorEngine();

            if (model == null)
            {
                #region No Model, only compile

                using var razorAssembly = await razorEngine.CompileAsync<RazorEngineTemplate<TModel>>(
                    code, builder => builder.AddDefaults(_options));

                return String.Empty;

                #endregion
            }
            else
            {
                #region Compile and Run

                if (IsCompilationCached(razorCacheId, model.GetType()))
                {
                    using var ms = new MemoryStream(_binaryCache.GetBytes(razorCacheId.ToRazorAssemblyFilename(), CacheNamespace));
                    using var cachedRazorAssembly = await RazorAssemblyUtility.LoadFromStreamAsync<RazorEngineTemplate<TModel>>(ms);

                    return await cachedRazorAssembly.RunAsync(instance => { instance.Model = model; });
                }

                using var razorAssembly = await razorEngine.CompileAsync<RazorEngineTemplate<TModel>>(
                    code, builder => builder.AddDefaults(_options));

                if (_options.RunGarbageCollectAfterCompile)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }

                if (!String.IsNullOrEmpty(razorCacheId))
                {
                    using var ms = new MemoryStream();
                    await razorAssembly.SaveToStreamAsync(ms);

                    using (var mutex = await FuzzyMutexAsync.LockAsync(razorCacheId))
                    {
                        _binaryCache.SetBytes(razorCacheId.ToRazorAssemblyFilename(), ms.ToArray(), CacheNamespace);
                    }
            }

                return await razorAssembly.RunAsync(instance => { instance.Model = model; });

                #endregion
            }
        }

        catch (RazorEngineCompilationException ex)
        {
            var errors = new List<RazorCompileError>();

            if (ex.Errors != null)
            {
                foreach (var error in ex.Errors)
                {
                    string codeLine = ex.GeneratedCode.GetLine(error.Location.GetLineSpan().StartLinePosition.Line + 1);  // +1 ... first line ist Comment?

                    errors.Add(new RazorCompileError()
                    {
                        IsWarning = error.Severity != Microsoft.CodeAnalysis.DiagnosticSeverity.Error,
                        ErrorText = error.GetMessage()/*+" Stacktrace"+ex.StackTrace*/,
                        Line = error.Location.GetLineSpan().StartLinePosition.Line,
                        Column = error.Location.GetLineSpan().StartLinePosition.Character,
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
}
