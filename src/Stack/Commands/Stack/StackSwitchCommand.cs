using System.CommandLine;
using Spectre.Console;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class StackSwitchCommand : Command
{
    public StackSwitchCommand() : base("switch", "Switch to a branch in a stack.")
    {
        Add(CommonOptions.Branch);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var handler = new StackSwitchCommandHandler(
            InputProvider,
            new GitClient(StdErrLogger, new GitClientSettings(Verbose, WorkingDirectory)),
            new FileStackConfig());

        await handler.Handle(new StackSwitchCommandInputs(
            parseResult.GetValue(CommonOptions.Branch)));
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
        var stackData = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();
        var currentBranch = gitClient.GetCurrentBranch();

        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();
        var allBranchesInStacks = stacksForRemote.SelectMany(s => s.AllBranchNames).Distinct().ToArray();
        var branchesThatExistLocally = gitClient.GetBranchesThatExistLocally(allBranchesInStacks);

        var branchSelection = inputs.Branch ?? inputProvider.SelectGrouped(
            Questions.SelectBranch,
            stacksForRemote
                .OrderByCurrentStackThenByName(currentBranch)
                .Select(s => new ChoiceGroup<string>(s.Name, [s.SourceBranch, .. s.AllBranchNames.Where(b => branchesThatExistLocally.Contains(b))]))
                .ToArray());

        if (inputs.Branch is not null && !gitClient.DoesLocalBranchExist(branchSelection))
            throw new InvalidOperationException($"Branch '{branchSelection}' does not exist.");

        gitClient.ChangeBranch(branchSelection);
    }
}