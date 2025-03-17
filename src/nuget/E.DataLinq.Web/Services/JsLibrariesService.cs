using E.DataLinq.Core.Models;
using Microsoft.Extensions.Options;
using System.Linq;

namespace E.DataLinq.Web.Services;
public class JsLibrariesService
{
    private readonly JsLibrariesServiceOptions _options;

    public JsLibrariesService(IOptions<JsLibrariesServiceOptions> options)
    {
        _options = options.Value;
    }

    public JsLibrary[] Libraries => _options.JsLibibraries.ToArray();
}
