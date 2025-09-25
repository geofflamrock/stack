using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Stack.Commands.Helpers;
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

        var hasCurrentStack = response.Stacks.Any(s => s.IsCurrent);

        foreach (var stack in response.Stacks)
        {
            await OutputProvider.WriteMessage($"{(stack.IsCurrent ? "* " : hasCurrentStack ? "  " : "")}{stack.Name.Stack()} {$"({stack.SourceBranch})".Muted()} {stack.BranchCount} {(stack.BranchCount == 1 ? "branch" : "branches")}", cancellationToken);
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
public record ListStacksCommandResponseItem(string Name, string SourceBranch, int BranchCount, bool IsCurrent);

public class ListStacksCommandHandler(IStackConfig stackConfig, IGitClientFactory gitClientFactory, CliExecutionContext executionContext)
    : CommandHandlerBase<ListStacksCommandInputs, ListStacksCommandResponse>
{
    public override async Task<ListStacksCommandResponse> Handle(ListStacksCommandInputs inputs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var stackData = stackConfig.Load();

        var gitClient = gitClientFactory.Create(executionContext.WorkingDirectory);
        var remoteUri = gitClient.GetRemoteUri();
        var currentBranch = gitClient.GetCurrentBranch();

        if (remoteUri is null)
        {
            return new ListStacksCommandResponse([]);
        }

        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        Config.Stack? currentStack = null;

        var stacksContainingBranch = stacksForRemote
            .Where(stack => StackContainsBranch(stack, currentBranch))
            .ToList();

        if (stacksContainingBranch.Count == 1)
        {
            currentStack = stacksContainingBranch.First();
        }

        return new ListStacksCommandResponse([.. stacksForRemote
            .OrderBy(s => s.Name)
            .Select(s => new ListStacksCommandResponseItem(s.Name, s.SourceBranch, s.Branches.Count, currentStack is not null && currentStack == s))]);

        static bool StackContainsBranch(Config.Stack stack, string branchName)
        {
            if (stack.SourceBranch.Equals(branchName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return stack.AllBranchNames.Any(b => b.Equals(branchName, StringComparison.OrdinalIgnoreCase));
        }
    }
}