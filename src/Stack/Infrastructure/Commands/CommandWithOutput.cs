using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Stack.Commands;

public abstract class CommandWithOutput<TSettings, TResponse> : Command<TSettings>
    where TSettings : CommandWithOutputSettingsBase
    where TResponse : notnull
{
    public override async Task<int> ExecuteAsync(CommandContext context, TSettings settings)
    {
        var response = await Execute(settings);
        WriteOutput(settings, response);

        return 0;
    }

    protected override abstract Task<TResponse> Execute(TSettings settings);

    protected abstract void WriteDefaultOutput(TResponse response);

    protected virtual void WriteJsonOutput(TResponse response, JsonSerializerOptions options)
    {
        var json = JsonSerializer.Serialize(response, options);
        StdOut.WriteLine(json);
    }

    private void WriteOutput(TSettings settings, TResponse response)
    {
        if (settings.Json)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = settings.Pretty
            };

            WriteJsonOutput(response, options);
        }
        else
        {
            WriteDefaultOutput(response);
        }
    }
}

