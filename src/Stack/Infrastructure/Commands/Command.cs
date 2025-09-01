using System.CommandLine;
using Microsoft.Extensions.Logging;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Commands;

public abstract class Command : System.CommandLine.Command
{
    protected ILogger Logger;
    protected IInputProvider InputProvider;
    protected IDisplayProvider DisplayProvider;
    protected string? WorkingDirectory;
    protected bool Json;

    public Command(
        string name,
        string? description,
        ILogger logger,
        IDisplayProvider displayProvider,
        IInputProvider inputProvider,
        CliExecutionContext executionContext) : base(name, description)
    {
        Logger = logger;
        DisplayProvider = displayProvider;
        InputProvider = inputProvider;

        Add(CommonOptions.WorkingDirectory);
        Add(CommonOptions.Verbose);
        Add(CommonOptions.Json);

        SetAction(async (parseResult, cancellationToken) =>
        {
            WorkingDirectory = parseResult.GetValue(CommonOptions.WorkingDirectory);
            Json = parseResult.GetValue(CommonOptions.Json);

            executionContext.WorkingDirectory = WorkingDirectory;

            try
            {
                await Execute(parseResult, cancellationToken);
                return 0;
            }
            catch (ProcessException processException)
            {
                Logger.LogError(processException.Message);
                return processException.ExitCode;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
                return 1;
            }
        });
    }

    protected TextWriter StdOut => System.Console.Out;

    protected abstract Task Execute(ParseResult parseResult, CancellationToken cancellationToken);
}
