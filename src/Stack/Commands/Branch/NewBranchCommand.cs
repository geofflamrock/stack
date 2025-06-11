using System.ComponentModel;
using MoreLinq;
using MoreLinq.Extensions;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class NewBranchCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack to create the branch in.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("The name of the branch to create.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }

    [Description("The name of the parent branch to create the new branch from.")]
    [CommandOption("-p|--parent")]
    public string? Parent { get; init; }
}

public class NewBranchCommand : Command<NewBranchCommandSettings>
{
    protected override async Task Execute(NewBranchCommandSettings settings)
    {
        var handler = new NewBranchCommandHandler(
            InputProvider,
            StdErrLogger,
            new GitClient(StdErrLogger, settings.GetGitClientSettings()),
            new FileStackConfig());

        await handler.Handle(new NewBranchCommandInputs(settings.Stack, settings.Name, settings.Parent));
    }
}

public record NewBranchCommandInputs(string? StackName, string? BranchName, string? ParentBranchName)
{
    public static NewBranchCommandInputs Empty => new(null, null, null);
}

public class NewBranchCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
    IGitClient gitClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<NewBranchCommandInputs>
{
    public override async Task Handle(NewBranchCommandInputs inputs)
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

        var branchName = inputProvider.Text(logger, Questions.BranchName, inputs.BranchName, stack.GetDefaultBranchName());

        if (stack.AllBranchNames.Contains(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' already exists in stack '{stack.Name}'.");
        }

        if (gitClient.DoesLocalBranchExist(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' already exists locally.");
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

        var sourceBranchName = sourceBranch?.Name ?? stack.SourceBranch;

        logger.Information($"Creating branch {branchName.Branch()} from {sourceBranchName.Branch()} in stack {stack.Name.Stack()}");

        gitClient.CreateNewBranch(branchName, sourceBranchName);

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

        logger.Information($"Branch {branchName.Branch()} created.");

        try
        {
            logger.Information($"Pushing branch {branchName.Branch()} to remote repository");
            gitClient.PushNewBranch(branchName);
        }
        catch (Exception)
        {
            logger.Warning($"An error has occurred pushing branch {branchName.Branch()} to remote repository. Use {$"stack push --name \"{stack.Name}\"".Example()} to push the branch to the remote repository.");
        }

        gitClient.ChangeBranch(branchName);
    }
}