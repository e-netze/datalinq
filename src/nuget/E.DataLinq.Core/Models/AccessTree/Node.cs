using System.Collections.Generic;

namespace E.DataLinq.Core.Models.AccessTree;

public class Node
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool Selected { get; set; }
    public ICollection<Node> Children { get; } = new List<Node>();
}
