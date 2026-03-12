using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CommandParser
{
    private readonly Dictionary<string, ICommand> _commands;

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
            { "exit", new ExitCommand(terminal) }  
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