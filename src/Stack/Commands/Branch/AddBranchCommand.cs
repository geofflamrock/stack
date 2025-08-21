using System.CommandLine;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class AddBranchCommand : Command
{
    public AddBranchCommand() : base("add", "Add an existing branch to a stack.")
    {
        Add(CommonOptions.Stack);
        Add(CommonOptions.Branch);
        Add(CommonOptions.ParentBranch);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var handler = new AddBranchCommandHandler(
            InputProvider,
            StdErrLogger,
            new GitClient(StdErrLogger, new GitClientSettings(Verbose, WorkingDirectory)),
            new FileStackConfig());

        await handler.Handle(new AddBranchCommandInputs(
            parseResult.GetValue(CommonOptions.Stack),
            parseResult.GetValue(CommonOptions.Branch),
            parseResult.GetValue(CommonOptions.ParentBranch)));
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

        var parentBranchName = inputProvider.SelectParentBranch(logger, inputs.ParentBranchName, stack);

        if (parentBranchName != stack.SourceBranch)
        {
            sourceBranch = stack.GetAllBranches().FirstOrDefault(b => b.Name.Equals(parentBranchName, StringComparison.OrdinalIgnoreCase));
            if (sourceBranch is null)
            {
                throw new InvalidOperationException($"Branch '{parentBranchName}' not found in stack '{stack.Name}'.");
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