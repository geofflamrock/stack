using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;

namespace Stack.Commands;

public class OpenConfigCommandSettings : CommandSettingsBase
{
}

public class OpenConfigCommand : AsyncCommand<CommandSettingsBase>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CommandSettingsBase settings)
    {
        await Task.CompletedTask;
        var console = AnsiConsole.Console;
        var stackConfig = new StackConfig();

        var configPath = stackConfig.GetConfigPath();

        if (!File.Exists(configPath))
        {
            console.WriteLine("No config file found.");
            return 0;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = configPath,
            UseShellExecute = true
        });

        return 0;
    }
}
