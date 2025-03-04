using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class StackSwitchCommandSettings : CommandSettingsBase
{
    [Description("The name of the branch to switch to.")]
    [CommandOption("-b|--branch")]
    public string? Branch { get; init; }
}

public class StackSwitchCommand : Command<StackSwitchCommandSettings>
{
    protected override async Task Execute(StackSwitchCommandSettings settings)
    {
        var handler = new StackSwitchCommandHandler(
            InputProvider,
            new GitClient(StdErrLogger, settings.GetGitClientSettings()),
            new StackConfig());

        await handler.Handle(new StackSwitchCommandInputs(settings.Branch));
    }
}

public record StackSwitchCommandInputs(string? Branch);

public class StackSwitchCommandHandler(
    IInputProvider inputProvider,
    IGitClient gitClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<StackSwitchCommandInputs>
{
    public override async Task Handle(StackSwitchCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();
        var currentBranch = gitClient.GetCurrentBranch();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();
        var allBranchesInStacks = stacksForRemote.SelectMany(s => s.Branches).Distinct().ToArray();
        var branchesThatExistLocally = gitClient.GetBranchesThatExistLocally(allBranchesInStacks);

        var branchSelection = inputs.Branch ?? inputProvider.SelectGrouped(
            Questions.SelectBranch,
            stacksForRemote
                .OrderByCurrentStackThenByName(currentBranch)
                .Select(s => new ChoiceGroup<string>(s.Name, [s.SourceBranch, .. s.Branches.Where(b => branchesThatExistLocally.Contains(b))]))
                .ToArray());

        if (inputs.Branch is not null && !gitClient.DoesLocalBranchExist(branchSelection))
            throw new InvalidOperationException($"Branch '{branchSelection}' does not exist.");

        gitClient.ChangeBranch(branchSelection);
    }
}