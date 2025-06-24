using System.CommandLine;
using Spectre.Console;
using Stack.Infrastructure;

namespace Stack.Commands;

public abstract class Command : System.CommandLine.Command
{
    protected ILogger StdOutLogger;
    protected ILogger StdErrLogger;
    protected IInputProvider InputProvider;
    protected string? WorkingDirectory;
    protected bool Verbose;

    public Command(string name, string? description = null) : base(name, description)
    {
        var stdOutAnsiConsole = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(Console.Out),
        });
        var stdErrAnsiConsole = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(Console.Error),
        });
        StdOutLogger = new ConsoleLogger(stdOutAnsiConsole);
        StdErrLogger = new ConsoleLogger(stdErrAnsiConsole);
        InputProvider = new ConsoleInputProvider(stdErrAnsiConsole);

        Add(CommonOptions.WorkingDirectory);
        Add(CommonOptions.Verbose);

        SetAction(async (parseResult, cancellationToken) =>
        {
            WorkingDirectory = parseResult.GetValue(CommonOptions.WorkingDirectory);
            Verbose = parseResult.GetValue(CommonOptions.Verbose);

            await Execute(parseResult, cancellationToken);
            return 0;
        });
    }

    protected TextWriter StdOut => Console.Out;
    protected TextWriter StdErr => Console.Error;

    protected abstract Task Execute(ParseResult parseResult, CancellationToken cancellationToken);
}
