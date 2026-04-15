using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CommandParser
{
    private readonly Dictionary<string, ICommand> _commands;

    /// <summary>Lightweight constructor for the main-menu interactive prompt.</summary>
    public CommandParser(System.Action onBoot, System.Action onShutdown, System.Action onSettings)
    {
        _commands = new Dictionary<string, ICommand>
        {
            { "bootsystem", new BootSystemCommand(onBoot)       },
            { "shutdown",   new ShutdownCommand(onShutdown)     },
            { "settings",   new SettingsCommand(onSettings)     },
            { "help",       new MenuHelpCommand()               },
        };
    }

    public CommandParser(TerminalController terminal, VirtualFileSystem vfs)
    {
        _commands = new Dictionary<string, ICommand>
        {
            { "help", new HelpCommand() },
            { "echo", new EchoCommand() },
            { "clear", new ClearCommand(terminal) },
            { "ls", new LsCommand(vfs) },
            { "cd", new CdCommand(vfs) },
            { "cat", new CatCommand(vfs) },
            { "restart", new RestartCommand() }, 
            { "quit", new QuitCommand() },       
            { "scan", new ScanCommand(terminal) },
            { "ssh", new SshCommand(terminal) }, 
            { "unlock", new UnlockCommand(terminal) }, 
            { "exit", new ExitCommand(terminal) }  ,
            {"next", new NextCommand() },
            { "mainmenu", new MainMenuCommand() }
        };
    }

    public string ParseAndExecute(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        string[] parts = input.Trim().Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        string commandName = parts[0].ToLower();
        string[] args = parts.Skip(1).ToArray();

        if (_commands.TryGetValue(commandName, out ICommand command))
        {
            return command.Execute(args);
        }

        return $"Command not found: '{commandName}'";
    }
}