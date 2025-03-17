using E.DataLinq.Core.Models;
using E.DataLinq.Core.Services.Abstraction;
using System;

namespace E.DataLinq.Core.Extensions;

static public class DataLinqEndPointExtensions
{
    static public string GetConnectionString(this DataLinqEndPoint endPoint, IDataLinqEnvironmentService environmentService)
    {
        if (endPoint == null)
        {
            return String.Empty;
        }

        if (environmentService == null)
        {
            return endPoint?.ConnectionString;
        }

        return environmentService.GetConnectionString(endPoint);
    }
}
