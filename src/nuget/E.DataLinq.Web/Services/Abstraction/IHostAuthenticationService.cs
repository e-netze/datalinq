using E.DataLinq.Core;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services.Abstraction;

public interface IHostAuthenticationService
{
    IDataLinqUser GetUser(HttpContext httpContext);

    Task<string> ClientSideAuthObjectStringAsync(HttpContext httpContext);
    Task<IEnumerable<string>> AuthPrefixesAsync(HttpContext httpContext);
    Task<IEnumerable<string>> AuthAutocompleteAsync(HttpContext httpContext, string prefix, string term);
}
