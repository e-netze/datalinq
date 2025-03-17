using E.DataLinq.Core.Services.Abstraction;
using System;
using System.Collections.Generic;

namespace E.DataLinq.Core.Extensions;

public static class SelectResultProviderExtensions
{
    public static (object result, string contentType) TransformAny(this IEnumerable<ISelectResultProvider> selectResultProviders, string resultViewId, IDictionary<string, object>[] records)
    {
        foreach (ISelectResultProvider selectResultProvider in selectResultProviders)
        {
            if (resultViewId.Equals(selectResultProvider.ResultViewId, StringComparison.OrdinalIgnoreCase))
            {
                var transformed = selectResultProvider.Transform(records);
                if (transformed.result != null)
                {
                    return transformed;
                }
            }
        }

        return (null, null);
    }
}
