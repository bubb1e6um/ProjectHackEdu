public class VirtualFileSystem
{
    public DirectoryNode Root { get; }
    public DirectoryNode CurrentDirectory { get; set; }

    public VirtualFileSystem()
    {
        Root = new DirectoryNode("root");
        CurrentDirectory = Root;
        
        InitializeMockData();
    }

    private void InitializeMockData()
    {
        DirectoryNode bin = new DirectoryNode("bin");
        DirectoryNode logs = new DirectoryNode("logs");
        
        Root.AddChild(bin);
        Root.AddChild(logs);
        
        logs.AddChild(new FileNode("system.log", "System initialized. No errors."));
        logs.AddChild(new FileNode("security.log", "Warning: Unauthorized access attempt detected on port 22."));
    }
}