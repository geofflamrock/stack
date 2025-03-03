using Humanizer;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class ListStacksCommandSettings : CommandSettingsBase;

public class ListStacksCommand : CommandWithHandler<ListStacksCommandSettings, ListStacksCommandInputs, ListStacksCommandResponse>
{
    protected override ListStacksCommandInputs CreateInputs(ListStacksCommandSettings settings) => new();

    protected override CommandHandlerBase<ListStacksCommandInputs, ListStacksCommandResponse> CreateHandler(ListStacksCommandSettings settings)
        => new ListStacksCommandHandler(new StackConfig(), new GitClient(Logger, settings.GetGitClientSettings()));

    protected override void FormatOutput(ListStacksCommandSettings settings, ListStacksCommandResponse response)
    {
        if (response.Stacks.Count == 0)
        {
            Console.WriteLine("No stacks found for current repository.");
            return;
        }

        foreach (var stack in response.Stacks)
        {
            Console.MarkupLine($"{stack.Name.Stack()} {$"({stack.SourceBranch})".Muted()} {"branch".ToQuantity(stack.BranchCount)}");
        }
    }
}

public record ListStacksCommandInputs;
public record ListStacksCommandResponse(List<ListStacksCommandResponseItem> Stacks);
public record ListStacksCommandResponseItem(string Name, string SourceBranch, int BranchCount);

public class ListStacksCommandHandler(IStackConfig stackConfig, IGitClient gitClient) : CommandHandlerBase<ListStacksCommandInputs, ListStacksCommandResponse>
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