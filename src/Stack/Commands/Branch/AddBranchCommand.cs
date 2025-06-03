using System.ComponentModel;
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

    [Description("The name of the parent branch to add branch as a child of.")]
    [CommandOption("-p|--parent")]
    public string? Parent { get; init; }
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

        await handler.Handle(new AddBranchCommandInputs(settings.Stack, settings.Name, settings.Parent));
    }
}

public record AddBranchCommandInputs(string? StackName, string? BranchName, string? ParentBranchName)
{
    public static AddBranchCommandInputs Empty => new(null, null, null);
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

        if (stackData.SchemaVersion == SchemaVersion.V1 && inputs.ParentBranchName is not null)
        {
            throw new InvalidOperationException("Parent branches are not supported in stacks with schema version v1. Please migrate the stack to v2 format.");
        }

        var stack = inputProvider.SelectStack(logger, inputs.StackName, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.StackName}' not found.");
        }

        var branchName = inputProvider.SelectBranch(logger, inputs.BranchName, branches);

        if (stack.AllBranchNames.Contains(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' already exists in stack '{stack.Name}'.");
        }

        if (!gitClient.DoesLocalBranchExist(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' does not exist locally.");
        }

        Branch? sourceBranch = null;

        if (stackData.SchemaVersion == SchemaVersion.V1)
        {
            // In V1 schema there is only a single set of branches, we always add to the end.
            sourceBranch = stack.GetAllBranches().LastOrDefault();
        }
        if (stackData.SchemaVersion == SchemaVersion.V2)
        {
            var parentBranchName = inputProvider.SelectParentBranch(logger, inputs.ParentBranchName, stack);

            if (parentBranchName != stack.SourceBranch)
            {
                sourceBranch = stack.GetAllBranches().FirstOrDefault(b => b.Name.Equals(parentBranchName, StringComparison.OrdinalIgnoreCase));
                if (sourceBranch is null)
                {
                    throw new InvalidOperationException($"Branch '{parentBranchName}' not found in stack '{stack.Name}'.");
                }
            }
        }

        logger.Information($"Adding branch {branchName.Branch()} to stack {stack.Name.Stack()}");

        if (sourceBranch is not null)
        {
            sourceBranch.Children.Add(new Branch(branchName, []));
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