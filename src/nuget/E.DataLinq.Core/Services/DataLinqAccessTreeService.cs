using E.DataLinq.Core.Models.AccessTree;
using E.DataLinq.Core.Services.Abstraction;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace E.DataLinq.Core.Services;

public class DataLinqAccessTreeService : IDataLinqAccessTreeService
{
    public Task<Tree> GetTree(string route) => Task.FromResult<Tree>(null);

    public Task<bool> DeleteTree(string route) => Task.FromResult(true);

    public Task<bool> SetSelectedTreeNodes(string route, IEnumerable<string> roles) => Task.FromResult(true);
}
