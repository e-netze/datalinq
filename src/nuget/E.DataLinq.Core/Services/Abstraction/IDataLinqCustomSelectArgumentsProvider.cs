using System.Collections.Specialized;

namespace E.DataLinq.Core.Services.Abstraction;

public interface IDataLinqCustomSelectArgumentsProvider
{
    public NameValueCollection CustomArguments();

    public bool OverrideExisting { get; }

    public string ExclusivePrefix { get; }
}
