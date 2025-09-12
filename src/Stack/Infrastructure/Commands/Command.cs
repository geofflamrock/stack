using System.CommandLine;
using Microsoft.Extensions.Logging;
using Spectre.Console;
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
    protected bool Verbose;

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
        Add(CommonOptions.Debug);
        Add(CommonOptions.Verbose);
        Add(CommonOptions.Json);

        SetAction(async (parseResult, cancellationToken) =>
        {
            WorkingDirectory = parseResult.GetValue(CommonOptions.WorkingDirectory);

            executionContext.WorkingDirectory = WorkingDirectory;

            try
            {
                await Execute(parseResult, cancellationToken);
                return 0;
            }
            catch (ProcessException processException)
            {
                Logger.ProcessExceptionCommandDetails(
                    Markup.Escape(processException.FilePath),
                    Markup.Escape(processException.Command));
                Logger.ProcessExceptionErrorMessage(Markup.Escape(processException.Message));
                return processException.ExitCode;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception ex)
            {
                Logger.ErrorMessage(Markup.Escape(ex.Message));
                return 1;
            }
        });
    }

    protected TextWriter StdOut => System.Console.Out;

    protected abstract Task Execute(ParseResult parseResult, CancellationToken cancellationToken);
}

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(Level = LogLevel.Error, Message = "An error occurred running command \"{FilePath} {Command}\"")]
    public static partial void ProcessExceptionCommandDetails(this ILogger logger, string filePath, string command);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Message}")]
    public static partial void ProcessExceptionErrorMessage(this ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    public static partial void ErrorMessage(this ILogger logger, string message);
}
