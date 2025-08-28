using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Commands;

public abstract class CommandWithOutput<TResponse> : Command where TResponse : notnull
{
    protected CommandWithOutput(
        string name,
        string? description,
        ILogger logger,
        IDisplayProvider displayProvider,
        IInputProvider inputProvider,
        CliExecutionContext executionContext)
        : base(name, description, logger, displayProvider, inputProvider, executionContext)
    {
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var json = parseResult.GetValue(CommonOptions.Json);
        var response = await ExecuteAndReturnResponse(parseResult, cancellationToken);
        await WriteOutput(json, response, cancellationToken);
    }

    protected abstract Task<TResponse> ExecuteAndReturnResponse(ParseResult parseResult, CancellationToken cancellationToken);

    protected abstract Task WriteDefaultOutput(TResponse response, CancellationToken cancellationToken);

    protected abstract Task WriteJsonOutput(TResponse response, CancellationToken cancellationToken);

    private async Task WriteOutput(bool json, TResponse response, CancellationToken cancellationToken)
    {
        if (json)
        {
            await WriteJsonOutput(response, cancellationToken);
        }
        else
        {
            await WriteDefaultOutput(response, cancellationToken);
        }
    }
}