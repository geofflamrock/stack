using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

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
        ILogger<ListStacksCommand> logger,
        IDisplayProvider displayProvider,
        IInputProvider inputProvider,
        CliExecutionContext executionContext,
        ListStacksCommandHandler handler)
        : base("list", "List stacks.", logger, displayProvider, inputProvider, executionContext)
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
            await DisplayProvider.DisplayMessage("No stacks found for current repository.", cancellationToken);
            return;
        }

        foreach (var stack in response.Stacks)
        {
            await DisplayProvider.DisplayMessage($"{stack.Name.Stack()} {$"({stack.SourceBranch})".Muted()} {stack.BranchCount} {(stack.BranchCount == 1 ? "branch" : "branches")}", cancellationToken);
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

public class ListStacksCommandHandler(IStackConfig stackConfig, IGitClient gitClient)
    : CommandHandlerBase<ListStacksCommandInputs, ListStacksCommandResponse>
{
    public override async Task<ListStacksCommandResponse> Handle(ListStacksCommandInputs inputs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var stackData = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();

        if (remoteUri is null)
        {
            return new ListStacksCommandResponse([]);
        }

        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        return new ListStacksCommandResponse([.. stacksForRemote.Select(s => new ListStacksCommandResponseItem(s.Name.ToString(), s.SourceBranch, s.Branches.Count))]);
    }
}