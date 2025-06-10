using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;

namespace Stack.Commands;

public class OpenConfigCommandSettings : CommandSettingsBase
{
}

public class OpenConfigCommand : Command<CommandSettingsBase>
{
    protected override async Task Execute(CommandSettingsBase settings)
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
