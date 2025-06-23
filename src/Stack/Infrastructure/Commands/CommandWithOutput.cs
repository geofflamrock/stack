using System.Text.Json;
using Spectre.Console.Cli;

namespace Stack.Commands;

public abstract class CommandWithOutput<TSettings, TResponse> : Command<TSettings>
    where TSettings : CommandWithOutputSettingsBase
    where TResponse : notnull
{
    readonly JsonSerializerOptions jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public override async Task<int> ExecuteAsync(CommandContext context, TSettings settings)
    {
        var response = await Execute(settings);
        WriteOutput(settings, response);

        return 0;
    }

    protected override abstract Task<TResponse> Execute(TSettings settings);

    protected abstract void WriteDefaultOutput(TResponse response);

    protected abstract void WriteJsonOutput(TResponse response);

    private void WriteOutput(TSettings settings, TResponse response)
    {
        if (settings.Json)
        {
            WriteJsonOutput(response);
        }
        else
        {
            WriteDefaultOutput(response);
        }
    }
}

