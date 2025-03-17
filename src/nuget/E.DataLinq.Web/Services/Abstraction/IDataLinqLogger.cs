using Microsoft.AspNetCore.Http;
using System;

namespace E.DataLinq.Web.Services.Abstraction;

public interface IDataLinqLogger
{
    IDisposable CreatePerformanceLogger(HttpContext httpContext,
                                        string method,
                                        string dataLinqRoute,
                                        string dataLinqUsername);
}
