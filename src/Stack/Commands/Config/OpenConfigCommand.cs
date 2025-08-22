using System.CommandLine;
using System.Diagnostics;
using Spectre.Console;
using Stack.Config;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Commands;

public class OpenConfigCommand : Command
{
    private readonly IAnsiConsole console;
    private readonly IStackConfig stackConfig;

    public OpenConfigCommand(
        IStdOutLogger stdOutLogger,
        IStdErrLogger stdErrLogger,
        IInputProvider inputProvider,
        IGitClientSettingsUpdater gitClientSettingsUpdater,
        IGitHubClientSettingsUpdater gitHubClientSettingsUpdater,
        IAnsiConsole console,
        IStackConfig stackConfig) : base("open", "Open the configuration file in the default editor.", stdOutLogger, stdErrLogger, inputProvider, gitClientSettingsUpdater, gitHubClientSettingsUpdater)
    {
        this.console = console;
        this.stackConfig = stackConfig;
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

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
