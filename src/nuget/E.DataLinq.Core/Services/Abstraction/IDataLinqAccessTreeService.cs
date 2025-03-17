#nullable enable

using E.DataLinq.Core.Models.AccessTree;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace E.DataLinq.Core.Services.Abstraction;

public interface IDataLinqAccessTreeService
{
    Task<Tree?> GetTree(string route);
    Task<bool> DeleteTree(string route);
    Task<bool> SetSelectedTreeNodes(string route, IEnumerable<string> roles);
}
