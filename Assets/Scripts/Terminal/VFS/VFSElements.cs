using System.Collections.Generic;
using System.Linq;

public class FileNode : VFSNode
{
    public string Content { get; set; }

    public FileNode(string name, string content = "") : base(name)
    {
        Content = content;
    }

    public override int GetSize() => Content.Length;
}

public class DirectoryNode : VFSNode
{
    private readonly Dictionary<string, VFSNode> _children = new Dictionary<string, VFSNode>();
    

    public DirectoryNode(string name) : base(name)
    {
        Permissions = "drwxr-xr-x";
    }

    public void AddChild(VFSNode node)
    {
        node.Parent = this;
        _children[node.Name] = node;
    }

    public VFSNode GetChild(string name)
    {
        return _children.TryGetValue(name, out VFSNode node) ? node : null;
    }

    public IEnumerable<VFSNode> GetChildren() => _children.Values;

    public override int GetSize() => _children.Values.Sum(c => c.GetSize());
}