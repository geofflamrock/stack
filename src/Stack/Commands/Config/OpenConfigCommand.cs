using System.CommandLine;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Stack.Config;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Commands;

public class OpenConfigCommand : Command
{
    readonly IStackConfig stackConfig;

    public OpenConfigCommand(
        ILogger<OpenConfigCommand> logger,
        IDisplayProvider displayProvider,
        IInputProvider inputProvider,
        CliExecutionContext executionContext,
        IStackConfig stackConfig)
        : base("open", "Open the configuration file in the default editor.", logger, displayProvider, inputProvider, executionContext)
    {
        this.stackConfig = stackConfig;
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var configPath = stackConfig.GetConfigPath();

        if (!File.Exists(configPath))
        {
            Logger.NoConfigFileFound();
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

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "No config file found.")]
    public static partial void NoConfigFileFound(this ILogger logger);
}
