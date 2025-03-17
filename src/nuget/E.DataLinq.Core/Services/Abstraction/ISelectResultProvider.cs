using System.Collections.Generic;

namespace E.DataLinq.Core.Services.Abstraction;

public interface ISelectResultProvider
{
    string ResultViewId { get; }

    (object result, string contentType) Transform(IDictionary<string, object>[] records);
}
