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
    protected string? WorkingDirectory;
    protected bool Verbose;

    public Command(
        string name,
        string? description,
        IStdOutLogger stdOutLogger,
        IStdErrLogger stdErrLogger,
        IInputProvider inputProvider,
        CliExecutionContext executionContext) : base(name, description)
    {
        StdOutLogger = stdOutLogger;
        StdErrLogger = stdErrLogger;
        InputProvider = inputProvider;

        Add(CommonOptions.WorkingDirectory);
        Add(CommonOptions.Verbose);

        SetAction(async (parseResult, cancellationToken) =>
        {
            WorkingDirectory = parseResult.GetValue(CommonOptions.WorkingDirectory);
            Verbose = parseResult.GetValue(CommonOptions.Verbose);

            executionContext.Verbose = Verbose;
            executionContext.WorkingDirectory = WorkingDirectory;

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
            catch (OperationCanceledException)
            {
                return 0;
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
