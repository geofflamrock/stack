using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class AddBranchCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack to create the branch in.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("The name of the branch to add.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

public class AddBranchCommand : Command<AddBranchCommandSettings>
{
    protected override async Task Execute(AddBranchCommandSettings settings)
    {
        var handler = new AddBranchCommandHandler(
            InputProvider,
            StdErrLogger,
            new GitClient(StdErrLogger, settings.GetGitClientSettings()),
            new FileStackConfig());

        await handler.Handle(new AddBranchCommandInputs(settings.Stack, settings.Name));
    }
}

public record AddBranchCommandInputs(string? StackName, string? BranchName)
{
    public static AddBranchCommandInputs Empty => new(null, null);
}

public class AddBranchCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
    IGitClient gitClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<AddBranchCommandInputs>
{
    public override async Task Handle(AddBranchCommandInputs inputs)
    {
        await Task.CompletedTask;

        var remoteUri = gitClient.GetRemoteUri();
        var currentBranch = gitClient.GetCurrentBranch();
        var branches = gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate();

        var stackData = stackConfig.Load();

        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            logger.Information("No stacks found for current repository.");
            return;
        }

        var stack = inputProvider.SelectStack(logger, inputs.StackName, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.StackName}' not found.");
        }

        var deepestChildBranchFromFirstTree = stack.GetDeepestChildBranchFromFirstTree();
        var sourceBranch = deepestChildBranchFromFirstTree?.Name ?? stack.SourceBranch;
        var branchName = inputProvider.SelectBranch(logger, inputs.BranchName, branches);

        if (stack.AllBranchNames.Contains(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' already exists in stack '{stack.Name}'.");
        }

        if (!gitClient.DoesLocalBranchExist(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' does not exist locally.");
        }

        logger.Information($"Adding branch {branchName.Branch()} to stack {stack.Name.Stack()}");

        if (deepestChildBranchFromFirstTree is not null)
        {
            // If the stack has branches, we add the new branch to the first branch's children
            deepestChildBranchFromFirstTree.Children.Add(new Branch(branchName, []));
        }
        else
        {
            // If the stack has no branches, we create a new branch entry
            stack.Branches.Add(new Branch(branchName, []));
        }

        stackConfig.Save(stackData);

        logger.Information($"Branch added");
    }
}