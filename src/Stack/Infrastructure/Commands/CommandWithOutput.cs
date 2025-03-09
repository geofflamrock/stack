using Spectre.Console;
using Spectre.Console.Cli;

namespace Stack.Commands;

public abstract class CommandWithOutput<TSettings, TResponse> : Command<TSettings>
    where TSettings : CommandSettingsBase
    where TResponse : notnull
{
    public override async Task<int> ExecuteAsync(CommandContext context, TSettings settings)
    {
        var response = await Execute(settings);
        WriteOutput(settings, response);

        return 0;
    }

    protected override abstract Task<TResponse> Execute(TSettings settings);

    protected abstract void WriteOutput(TSettings settings, TResponse response);
}

