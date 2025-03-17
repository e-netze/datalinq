using System;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services.Abstraction;

public interface IRazorCompileEngineService
{
    string EngineId { get; }

    Task<string> RunCompile<TModel>(string code, string razorCacheId, TModel model = null) where TModel : class;

    object RawString(string str);

    bool IsCompilationCached(string razorCacheId, Type modelType);
}
