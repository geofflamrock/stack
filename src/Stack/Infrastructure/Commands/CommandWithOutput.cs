using System.CommandLine;
using System.Text.Json;

namespace Stack.Commands;

public abstract class CommandWithOutput<TResponse> : Command where TResponse : notnull
{
    protected CommandWithOutput(string name, string? description = null) : base(name, description)
    {
        Add(CommonOptions.Json);
    }

    readonly JsonSerializerOptions jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    protected override async Task<int> Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var json = parseResult.GetValue(CommonOptions.Json);
        var response = await ExecuteAndReturnResponse(parseResult, cancellationToken);
        WriteOutput(json, response);

        return 0;
    }

    protected abstract Task<TResponse> ExecuteAndReturnResponse(ParseResult parseResult, CancellationToken cancellationToken);

    protected abstract void WriteDefaultOutput(TResponse response);

    protected abstract void WriteJsonOutput(TResponse response);

    private void WriteOutput(bool json, TResponse response)
    {
        if (json)
        {
            WriteJsonOutput(response);
        }
        else
        {
            WriteDefaultOutput(response);
        }
    }
}