public abstract class VFSNode
{
    public string Name { get; }
    public DirectoryNode Parent { get; set; }
    
    public string Permissions { get; set; } = "-rw-r--r--";
    public string Owner { get; set; } = "root";
    public string Date { get; set; } = "14 Feb 01:36";

    protected VFSNode(string name)
    {
        Name = name;
    }

    public abstract int GetSize();
}