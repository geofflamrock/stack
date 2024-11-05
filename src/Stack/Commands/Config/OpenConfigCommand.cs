using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;

namespace Stack.Commands;

internal class OpenConfigCommandSettings : CommandSettingsBase
{
}

internal class OpenConfigCommand : AsyncCommand<CommandSettingsBase>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CommandSettingsBase settings)
    {
        await Task.CompletedTask;

        var configPath = StackConfig.GetConfigPath();

        if (!File.Exists(configPath))
        {
            AnsiConsole.WriteLine("No config file found.");
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
