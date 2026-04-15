using System.Text;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HelpCommand : ICommand
{
    public string Execute(string[] args)
    {
        return "Available commands: help, echo, clear";
    }
}

public class EchoCommand : ICommand
{
    public string Execute(string[] args)
    {
        if (args == null || args.Length == 0) return string.Empty;
        
        return string.Join(" ", args);
    }
}

public class ClearCommand : ICommand
{
    private readonly TerminalController _terminal;

    public ClearCommand(TerminalController terminal)
    {
        _terminal = terminal;
    }

    public string Execute(string[] args)
    {
        _terminal.ClearLog();
        return string.Empty;
    }
}

public class LsCommand : ICommand
{
    private readonly VirtualFileSystem _vfs;

    public LsCommand(VirtualFileSystem vfs)
    {
        _vfs = vfs;
    }

    public string Execute(string[] args)
    {
        var children = _vfs.CurrentDirectory.GetChildren();
        if (!children.Any()) return string.Empty;

        StringBuilder sb = new StringBuilder();
        foreach (var child in children)
        {
            // Формируем красивую строку
            string sizeStr = child.GetSize().ToString().PadLeft(4);
            string line = $"{child.Permissions}  {child.Owner}  {sizeStr}  {child.Date}  ";
            
            if (child is DirectoryNode)
            {
                sb.AppendLine($"{line}<color=#55FF55>{child.Name}</color>");
            }
            else
            {
                sb.AppendLine($"{line}{child.Name}");
            }
        }
        return sb.ToString().TrimEnd();
    
    }
}

public class CdCommand : ICommand
{
    private readonly VirtualFileSystem _vfs;

    public CdCommand(VirtualFileSystem vfs)
    {
        _vfs = vfs;
    }

    public string Execute(string[] args)
    {
        if (args.Length == 0) return string.Empty;
        
        string target = args[0];

        if (target == "~")
        {
            _vfs.CurrentDirectory = _vfs.Root;
            return string.Empty;
        }

        if (target.StartsWith("~/"))
        {
            target = target.Substring(2);
            _vfs.CurrentDirectory = _vfs.Root;
            
            if (string.IsNullOrEmpty(target)) return string.Empty; 
        }

        if (target == "..")
        {
            if (_vfs.CurrentDirectory.Parent != null)
                _vfs.CurrentDirectory = _vfs.CurrentDirectory.Parent;
            return string.Empty;
        }

        var child = _vfs.CurrentDirectory.GetChild(target);
        if (child == null) return $"cd: {target}: No such file or directory";
        
        if (child is DirectoryNode dirNode)
        {
            _vfs.CurrentDirectory = dirNode;
            return string.Empty;
        }

        return $"cd: {target}: Not a directory";
    }
}

public class CatCommand : ICommand
{
    private readonly VirtualFileSystem _vfs;

    public CatCommand(VirtualFileSystem vfs)
    {
        _vfs = vfs;
    }

    public string Execute(string[] args)
    {
        if (args.Length == 0) return "cat: missing operand";
        
        string target = args[0];
        var child = _vfs.CurrentDirectory.GetChild(target);

        if (child == null) return $"cat: {target}: No such file or directory";
        
        if (child is FileNode fileNode)
        {
            return fileNode.Content;
        }

        return $"cat: {target}: Is a directory";
    }

}

public class RestartCommand : ICommand
{
    public string Execute(string[] args)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartLevel();
            return "Rebooting system...";
        }
        return "Error: GameManager not found.";
    }
}

public class QuitCommand : ICommand
{
    public string Execute(string[] args)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.QuitGame();
            return "Terminating connection...";
        }
        return "Error: GameManager not found.";
    }
}

public class NextCommand : ICommand
{
    public string Execute(string[] args)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadNextLevel();
            return "Loading next sector...";
        }
        return "Error: System routing failed.";
    }
}

public class ScanCommand : ICommand
{
    private readonly TerminalController _terminal;
    public ScanCommand(TerminalController terminal) { _terminal = terminal; }

    public string Execute(string[] args)
    {
        var playerMovement = Object.FindAnyObjectByType<GridMovement>();
        if (playerMovement == null) return "Error: Local physical module missing.";
        
        Vector3 playerPos = playerMovement.transform.position;
        string result = "Scanning local area (Range: 1 block)...\n";
        int foundCount = 0;
        float scanRadius = 2f; 

        foreach(var node in _terminal.GlobalNetwork.GetAllNodes())
        {
            if (node.IP == "127.0.0.1" || node.PhysicalTransform == null) continue;

            Vector3 p1 = new Vector3(playerPos.x, 0, playerPos.z);
            Vector3 p2 = new Vector3(node.PhysicalTransform.position.x, 0, node.PhysicalTransform.position.z);
            
            float dist = Vector3.Distance(p1, p2);
            
            if (dist <= scanRadius)
            {
                string status = node.IsLocked ? "<color=red>[LOCKED]</color>" : "<color=green>[OPEN]</color>";
                result += $"- {node.IP}  ({node.DeviceName})  {status}\n";
                foundCount++;
            }
        }
        
        if (foundCount == 0) return result + "No devices found nearby.";
        return result.TrimEnd();
    }
}

public class SshCommand : ICommand
{
    private readonly TerminalController _terminal;
    public SshCommand(TerminalController terminal) { _terminal = terminal; }

    public string Execute(string[] args)
    {
        if (args.Length == 0) return "ssh: missing IP address";
        
        string targetIP = args[0];
        
        // Find IP in the global network
        NetworkNode targetNode = _terminal.GlobalNetwork.GetNode(targetIP);
        
        if (targetNode == null || targetIP == "127.0.0.1") 
            return $"ssh: connect to host {targetIP}: Connection refused";

        _terminal.ActiveConnection = targetNode;
        return $"Connected to {targetNode.DeviceName} ({targetIP}).";
    }
}

public class UnlockCommand : ICommand
{
    private readonly TerminalController _terminal;
    public UnlockCommand(TerminalController terminal) { _terminal = terminal; }

    public string Execute(string[] args)
    {
        if (_terminal.ActiveConnection.IP == "127.0.0.1")
            return "unlock: Cannot run on local machine.";

        if (!_terminal.ActiveConnection.IsLocked)
            return "System is already unlocked.";

        // Hack the node and invoke the 3D event
        _terminal.ActiveConnection.IsLocked = false;
        _terminal.ActiveConnection.OnUnlock?.Invoke();
        
        return "SUCCESS: Security bypassed. Target unlocked.";
    }
}

public class ExitCommand : ICommand
{
    private readonly TerminalController _terminal;
    public ExitCommand(TerminalController terminal) { _terminal = terminal; }

    public string Execute(string[] args)
    {
        if (_terminal.ActiveConnection.IP == "127.0.0.1")
        {
            // Optional: quit the game or close terminal
            return "Already on local machine.";
        }

        string oldIP = _terminal.ActiveConnection.IP;
        _terminal.ActiveConnection = _terminal.LocalNode;
        return $"Connection to {oldIP} closed.";
    }
}

// ── Main-menu commands ────────────────────────────────────────────────────────
// These are registered in CommandParser via the menu constructor and executed
// from the interactive prompt in MainMenuController.

public class BootSystemCommand : ICommand
{
    readonly System.Action _onBoot;
    public BootSystemCommand(System.Action onBoot) => _onBoot = onBoot;

    public string Execute(string[] args)
    {
        _onBoot?.Invoke();
        return "// BOOTING SYSTEM...";
    }
}

public class ShutdownCommand : ICommand
{
    readonly System.Action _onShutdown;
    public ShutdownCommand(System.Action onShutdown) => _onShutdown = onShutdown;

    public string Execute(string[] args)
    {
        _onShutdown?.Invoke();
        return "// INITIATING SHUTDOWN...";
    }
}

public class SettingsCommand : ICommand
{
    readonly System.Action _onSettings;
    public SettingsCommand(System.Action onSettings) => _onSettings = onSettings;

    public string Execute(string[] args)
    {
        _onSettings?.Invoke();
        return string.Empty;
    }
}

public class MenuHelpCommand : ICommand
{
    public string Execute(string[] args)
    {
        return "// COMMANDS: bootsystem | shutdown | settings | help";
    }
}

public class MainMenuCommand : ICommand
{
    public string Execute(string[] args)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
        return "// RETURNING TO MAIN TERMINAL...";
    }
}