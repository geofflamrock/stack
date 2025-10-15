using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Stack.Commands.Helpers;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;
using Stack.Persistence;

namespace Stack.Commands;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ListStacksCommandResponse))]
[JsonSerializable(typeof(ListStacksCommandResponseItem))]
internal partial class ListStacksCommandJsonSerializerContext : JsonSerializerContext
{
}

public class ListStacksCommand : CommandWithOutput<ListStacksCommandResponse>
{
    private readonly ListStacksCommandHandler handler;

    public ListStacksCommand(
        ListStacksCommandHandler handler,
        CliExecutionContext executionContext,
        IInputProvider inputProvider,
        IOutputProvider outputProvider,
        ILogger<ListStacksCommand> logger)
        : base("list", "List stacks.", executionContext, inputProvider, outputProvider, logger)
    {
        this.handler = handler;
    }

    protected override async Task<ListStacksCommandResponse> ExecuteAndReturnResponse(ParseResult parseResult, CancellationToken cancellationToken)
    {
        return await handler.Handle(new ListStacksCommandInputs(), cancellationToken);
    }

    protected override async Task WriteDefaultOutput(ListStacksCommandResponse response, CancellationToken cancellationToken)
    {
        if (response.Stacks.Count == 0)
        {
            Logger.NoStacksForRepository();
            return;
        }

        foreach (var stack in response.Stacks)
        {
            await OutputProvider.WriteMessage($"{stack.Name.Stack()} {$"({stack.SourceBranch})".Muted()} {stack.BranchCount} {(stack.BranchCount == 1 ? "branch" : "branches")}", cancellationToken);
        }
    }

    protected override async Task WriteJsonOutput(ListStacksCommandResponse response, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(response, typeof(ListStacksCommandResponse), ListStacksCommandJsonSerializerContext.Default);
        await StdOut.WriteLineAsync(json.AsMemory(), cancellationToken);
    }
}

public record ListStacksCommandInputs;
public record ListStacksCommandResponse(List<ListStacksCommandResponseItem> Stacks);
public record ListStacksCommandResponseItem(string Name, string SourceBranch, int BranchCount);

public class ListStacksCommandHandler(IStackRepository repository)
    : CommandHandlerBase<ListStacksCommandInputs, ListStacksCommandResponse>
{
    public override async Task<ListStacksCommandResponse> Handle(ListStacksCommandInputs inputs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var stacksForRemote = repository.GetStacks();

        return new ListStacksCommandResponse([.. stacksForRemote.Select(s => new ListStacksCommandResponseItem(s.Name, s.SourceBranch, s.Branches.Count))]);
    }
}