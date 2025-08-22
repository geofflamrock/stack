using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Spectre.Console;
using Stack.Config;

namespace Stack.Commands;

public class OpenConfigCommand : Command
{
    public OpenConfigCommand(IServiceProvider serviceProvider) : base("open", "Open the configuration file in the default editor.", serviceProvider)
    {
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var console = ServiceProvider.GetRequiredService<IAnsiConsole>();
        var stackConfig = ServiceProvider.GetRequiredService<IStackConfig>();

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
