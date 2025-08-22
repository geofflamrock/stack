using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using MoreLinq;
using MoreLinq.Extensions;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class NewBranchCommand : Command
{
    public NewBranchCommand() : base("new", "Create a new branch in a stack.")
    {
        Add(CommonOptions.Stack);
        Add(CommonOptions.Branch);
        Add(CommonOptions.ParentBranch);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var gitClient = ServiceProvider.GetRequiredService<IGitClient>();
        var stackConfig = ServiceProvider.GetRequiredService<IStackConfig>();

        var handler = new NewBranchCommandHandler(
            InputProvider,
            StdErrLogger,
            gitClient,
            stackConfig);

        await handler.Handle(new NewBranchCommandInputs(
            parseResult.GetValue(CommonOptions.Stack),
            parseResult.GetValue(CommonOptions.Branch),
            parseResult.GetValue(CommonOptions.ParentBranch)));
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

        var stack = inputProvider.SelectStack(logger, inputs.StackName, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.StackName}' not found.");
        }

        var branchName = inputProvider.Text(logger, Questions.BranchName, inputs.BranchName);

        if (stack.AllBranchNames.Contains(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' already exists in stack '{stack.Name}'.");
        }

        if (gitClient.DoesLocalBranchExist(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' already exists locally.");
        }

        Branch? sourceBranch = null;

        var parentBranchName = inputProvider.SelectParentBranch(logger, inputs.ParentBranchName, stack);

        if (parentBranchName != stack.SourceBranch)
        {
            sourceBranch = stack.GetAllBranches().FirstOrDefault(b => b.Name.Equals(parentBranchName, StringComparison.OrdinalIgnoreCase));
            if (sourceBranch is null)
            {
                throw new InvalidOperationException($"Branch '{parentBranchName}' not found in stack '{stack.Name}'.");
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