using System.Collections.Generic;

namespace E.DataLinq.Core;

public interface IDataLinqUser
{
    string Username { get; }

    IEnumerable<string> Userroles { get; }
    IEnumerable<string> Claims { get; }
}
