using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        IStdOutLogger stdOutLogger,
        IStdErrLogger stdErrLogger,
        IInputProvider inputProvider,
        IGitClientSettingsUpdater gitClientSettingsUpdater,
        IGitHubClientSettingsUpdater gitHubClientSettingsUpdater,
        ListStacksCommandHandler handler) 
        : base("list", "List stacks.", stdOutLogger, stdErrLogger, inputProvider, gitClientSettingsUpdater, gitHubClientSettingsUpdater)
    {
        this.handler = handler;
    }

    protected override async Task<ListStacksCommandResponse> ExecuteAndReturnResponse(ParseResult parseResult, CancellationToken cancellationToken)
    {
        return await handler.Handle(new ListStacksCommandInputs());
    }

    protected override void WriteDefaultOutput(ListStacksCommandResponse response)
    {
        if (response.Stacks.Count == 0)
        {
            StdErr.WriteLine("No stacks found for current repository.");
            return;
        }

        foreach (var stack in response.Stacks)
        {
            StdOutLogger.Information($"{stack.Name.Stack()} {$"({stack.SourceBranch})".Muted()} {stack.BranchCount} {(stack.BranchCount == 1 ? "branch" : "branches")}");
        }
    }

    protected override void WriteJsonOutput(ListStacksCommandResponse response)
    {
        var json = JsonSerializer.Serialize(response, typeof(ListStacksCommandResponse), ListStacksCommandJsonSerializerContext.Default);
        StdOut.WriteLine(json);
    }
}

public record ListStacksCommandInputs;
public record ListStacksCommandResponse(List<ListStacksCommandResponseItem> Stacks);
public record ListStacksCommandResponseItem(string Name, string SourceBranch, int BranchCount);

public class ListStacksCommandHandler(IStackConfig stackConfig, IGitClient gitClient)
    : CommandHandlerBase<ListStacksCommandInputs, ListStacksCommandResponse>
{
    public override async Task<ListStacksCommandResponse> Handle(ListStacksCommandInputs inputs)
    {
        await Task.CompletedTask;

        var stackData = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();

        if (remoteUri is null)
        {
            return new ListStacksCommandResponse([]);
        }

        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        return new ListStacksCommandResponse([.. stacksForRemote.Select(s => new ListStacksCommandResponseItem(s.Name, s.SourceBranch, s.Branches.Count))]);
    }
}