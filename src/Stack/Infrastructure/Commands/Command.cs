using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Commands;

public abstract class Command : System.CommandLine.Command
{
    protected ILogger StdOutLogger;
    protected ILogger StdErrLogger;
    protected IInputProvider InputProvider;
    protected string? WorkingDirectory;
    protected bool Verbose;
    protected IServiceProvider ServiceProvider;

    public Command(string name, string? description = null) : base(name, description)
    {
        // Create a host to get the service provider
        var host = ServiceConfiguration.CreateHost();
        ServiceProvider = host.Services;

        // Get services from DI
        var stdErrAnsiConsole = ServiceProvider.GetRequiredService<IAnsiConsole>();
        var stdOutAnsiConsole = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(Console.Out),
        });
        
        StdOutLogger = new ConsoleLogger(stdOutAnsiConsole);
        StdErrLogger = ServiceProvider.GetRequiredService<ILogger>();
        InputProvider = ServiceProvider.GetRequiredService<IInputProvider>();

        Add(CommonOptions.WorkingDirectory);
        Add(CommonOptions.Verbose);

        SetAction(async (parseResult, cancellationToken) =>
        {
            WorkingDirectory = parseResult.GetValue(CommonOptions.WorkingDirectory);
            Verbose = parseResult.GetValue(CommonOptions.Verbose);

            // Update the settings for Git clients
            var gitClientUpdater = ServiceProvider.GetRequiredService<IGitClientSettingsUpdater>();
            gitClientUpdater.UpdateSettings(Verbose, WorkingDirectory);

            var gitHubClientUpdater = ServiceProvider.GetRequiredService<IGitHubClientSettingsUpdater>();
            gitHubClientUpdater.UpdateSettings(Verbose, WorkingDirectory);

            try
            {
                await Execute(parseResult, cancellationToken);
                return 0;
            }
            catch (ProcessException processException)
            {
                StdErrLogger.Error(processException.Message);
                return processException.ExitCode;
            }
            catch (Exception ex)
            {
                StdErrLogger.Error(ex.Message);
                return 1;
            }
        });
    }

    protected TextWriter StdOut => Console.Out;
    protected TextWriter StdErr => Console.Error;

    protected abstract Task Execute(ParseResult parseResult, CancellationToken cancellationToken);
}
