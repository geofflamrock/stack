using System.CommandLine;
using System.Diagnostics;
using Spectre.Console;
using Stack.Config;

namespace Stack.Commands;

public class OpenConfigCommand : Command
{
    public OpenConfigCommand() : base("open", "Open the configuration file in the default editor.")
    {
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var console = AnsiConsole.Console;
        var stackConfig = new FileStackConfig();

        var configPath = stackConfig.GetConfigPath();

        if (!File.Exists(configPath))
        {
            console.WriteLine("No config file found.");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = configPath,
            UseShellExecute = true
        });

        return;
    }
}
