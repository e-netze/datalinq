using E.DataLinq.Web.Services;
using E.DataLinq.Web.Services.Abstraction;
using System.Collections.Generic;
using System.Linq;

namespace E.DataLinq.Web.Extensions;
static internal class EnumerableExtentions
{
    static public IRazorCompileEngineService GetRazorEngineService(this IEnumerable<IRazorCompileEngineService> razorEngines, DataLinqOptions options, string viewCode)
    {
        return razorEngines.First(e => options.EngineId.Equals(e.EngineId, System.StringComparison.OrdinalIgnoreCase));
    }
}
