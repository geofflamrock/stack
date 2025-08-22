using System.CommandLine;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Commands;

public abstract class Command : System.CommandLine.Command
{
    protected IStdOutLogger StdOutLogger;
    protected IStdErrLogger StdErrLogger;
    protected IInputProvider InputProvider;
    protected IGitClientSettingsUpdater GitClientSettingsUpdater;
    protected IGitHubClientSettingsUpdater GitHubClientSettingsUpdater;
    protected string? WorkingDirectory;
    protected bool Verbose;

    public Command(
        string name, 
        string? description,
        IStdOutLogger stdOutLogger,
        IStdErrLogger stdErrLogger,
        IInputProvider inputProvider,
        IGitClientSettingsUpdater gitClientSettingsUpdater,
        IGitHubClientSettingsUpdater gitHubClientSettingsUpdater) : base(name, description)
    {
        StdOutLogger = stdOutLogger;
        StdErrLogger = stdErrLogger;
        InputProvider = inputProvider;
        GitClientSettingsUpdater = gitClientSettingsUpdater;
        GitHubClientSettingsUpdater = gitHubClientSettingsUpdater;

        Add(CommonOptions.WorkingDirectory);
        Add(CommonOptions.Verbose);

        SetAction(async (parseResult, cancellationToken) =>
        {
            WorkingDirectory = parseResult.GetValue(CommonOptions.WorkingDirectory);
            Verbose = parseResult.GetValue(CommonOptions.Verbose);

            // Update the settings for Git clients
            GitClientSettingsUpdater.UpdateSettings(Verbose, WorkingDirectory);
            GitHubClientSettingsUpdater.UpdateSettings(Verbose, WorkingDirectory);

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
