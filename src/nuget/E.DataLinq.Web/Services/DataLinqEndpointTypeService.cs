using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace E.DataLinq.Web.Services;

public class DataLinqEndpointTypeService
{
    private readonly ConcurrentDictionary<int, string> _types = null;

    public DataLinqEndpointTypeService(IOptionsMonitor<DataLinqOptions> optionsMonitor)
    {
        if (_types == null)
        {
            _types = new ConcurrentDictionary<int, string>();
        }

        if (optionsMonitor.CurrentValue != null)
        {
            foreach (var type in optionsMonitor.CurrentValue.SupportedEndPointTypes)
            {
                AddTypes(type);
            }
        }
    }

    public IDictionary<int, string> TypeDictionary => _types;

    public void AddTypes(Type enumType)
    {
        foreach (var value in Enum.GetValues(enumType))
        {
            if (!_types.ContainsKey((int)value))
            {
                _types.TryAdd((int)value, value.ToString());
            }
        }
    }
}
