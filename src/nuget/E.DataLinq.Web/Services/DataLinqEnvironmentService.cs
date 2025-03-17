using E.DataLinq.Core;
using E.DataLinq.Core.Models;
using E.DataLinq.Core.Services.Abstraction;
using Microsoft.Extensions.Options;
using System;

namespace E.DataLinq.Web.Services;

public class DataLinqEnvironmentService : IDataLinqEnvironmentService
{
    private readonly DataLinqOptions _options;

    public DataLinqEnvironmentService(IOptionsMonitor<DataLinqOptions> optionsMonitor)
    {
        _options = optionsMonitor.CurrentValue;
    }

    #region IDataLinqEnvironmentService

    public string GetConnectionString(DataLinqEndPoint endPoint)
    {
        switch (_options.EnvironmentType)
        {
            case DataLinqEnvironmentType.Test:
            case DataLinqEnvironmentType.Development:
                if (!String.IsNullOrEmpty(endPoint.ConnectionString_DevTest))
                {
                    return endPoint.ConnectionString_DevTest;
                }
                else
                {
                    return endPoint.ConnectionString;
                }

            default:
                return endPoint.ConnectionString;

        }
    }

    #endregion
}
