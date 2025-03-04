using Humanizer;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class ListStacksCommandSettings : CommandSettingsBase;

public class ListStacksCommand : CommandWithOutput<ListStacksCommandSettings, ListStacksCommandResponse>
{
    protected override async Task<ListStacksCommandResponse> Handle(ListStacksCommandSettings settings)
    {
        var handler = new ListStacksCommandHandler(
            new StackConfig(),
            new GitClient(StdErrLogger, settings.GetGitClientSettings()));

        return await handler.Handle(new ListStacksCommandInputs());
    }

    protected override void WriteOutput(ListStacksCommandSettings settings, ListStacksCommandResponse response)
    {
        if (response.Stacks.Count == 0)
        {
            StdErr.WriteLine("No stacks found for current repository.");
            return;
        }

        foreach (var stack in response.Stacks)
        {
            StdErr.MarkupLine($"{stack.Name.Stack()} {$"({stack.SourceBranch})".Muted()} {"branch".ToQuantity(stack.BranchCount)}");
        }
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

        var stacks = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();

        if (remoteUri is null)
        {
            return new ListStacksCommandResponse([]);
        }

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        return new ListStacksCommandResponse([.. stacksForRemote.Select(s => new ListStacksCommandResponseItem(s.Name, s.SourceBranch, s.Branches.Count))]);
    }
}