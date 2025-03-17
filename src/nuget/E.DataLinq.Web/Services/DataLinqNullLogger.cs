using E.DataLinq.Web.Services.Abstraction;
using Microsoft.AspNetCore.Http;
using System;

namespace E.DataLinq.Web.Services;

public class DataLinqNullLogger : IDataLinqLogger
{
    public IDisposable CreatePerformanceLogger(HttpContext httpContext,
                                               string method,
                                               string dataLinqRoute,
                                               string dataLinqUsername)
    {
        return new PerfornamceNullLogger();
    }

    #region Classes

    private class PerfornamceNullLogger : IDisposable
    {
        public void Dispose()
        {

        }
    }

    #endregion
}
