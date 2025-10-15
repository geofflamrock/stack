using System.CommandLine;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;
using Stack.Persistence;

namespace Stack.Commands;

public class OpenConfigCommand : Command
{
    readonly IStackDataStore dataStore;

    public OpenConfigCommand(
        IStackDataStore dataStore,
        CliExecutionContext executionContext,
        IInputProvider inputProvider,
        IOutputProvider outputProvider,
        ILogger<OpenConfigCommand> logger)
        : base("open", "Open the configuration file in the default editor.", executionContext, inputProvider, outputProvider, logger)
    {
        this.dataStore = dataStore;
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var configPath = dataStore.GetConfigPath();

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
