using System.Collections.Generic;
using System.Linq;

namespace E.DataLinq.Core.Models.AccessTree;

public class Tree
{
    public ICollection<Node> Children { get; } = new List<Node>();

#if DEBUG
    static public Tree CreateDummy()
    {
        var tree = new Tree();

        tree.Children.Add(new Node()
        {
            Id = "gen",
            Name = "General",
            Description = "Free content and services",
            Selected = false
        });

        tree.Children.Last().Children.Add(new Node()
        {
            Id = "gen_more",
            Name = "More",
            Description = "Extra free content and services",
            Selected = true
        });

        tree.Children.Add(new Node()
        {
            Id = "secure",
            Name = "Secured",
            Description = "Sescured content and services",
            Selected = false
        });

        tree.Children.Last().Children.Add(new Node()
        {
            Id = "secure_write",
            Name = "Edit",
            Description = "Edit secure content and services",
        });

        tree.Children.Last().Children.Add(new Node()
        {
            Id = "secure_admin",
            Name = "Admin",
            Description = "Admin secure content and services",
        });

        return tree;
    }
#endif
}
