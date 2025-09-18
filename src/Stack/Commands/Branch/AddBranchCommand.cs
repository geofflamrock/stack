using System.CommandLine;
using Microsoft.Extensions.Logging;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Commands;

public class AddBranchCommand : Command
{
    private readonly AddBranchCommandHandler handler;

    public AddBranchCommand(
        AddBranchCommandHandler handler,
        CliExecutionContext executionContext,
        IInputProvider inputProvider,
        IOutputProvider outputProvider,
        ILogger<AddBranchCommand> logger)
        : base("add", "Add an existing branch to a stack.", executionContext, inputProvider, outputProvider, logger)
    {
        this.handler = handler;
        Add(CommonOptions.Stack);
        Add(CommonOptions.Branch);
        Add(CommonOptions.ParentBranch);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await handler.Handle(
            new AddBranchCommandInputs(
                parseResult.GetValue(CommonOptions.Stack),
                parseResult.GetValue(CommonOptions.Branch),
                parseResult.GetValue(CommonOptions.ParentBranch)),
            cancellationToken);
    }
}

public record AddBranchCommandInputs(string? StackName, string? BranchName, string? ParentBranchName)
{
    public static AddBranchCommandInputs Empty => new(null, null, null);
}

public class AddBranchCommandHandler(
    IInputProvider inputProvider,
    ILogger<AddBranchCommandHandler> logger,
    IGitClient gitClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<AddBranchCommandInputs>
{
    public override async Task Handle(AddBranchCommandInputs inputs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var remoteUri = gitClient.GetRemoteUri();
        var currentBranch = gitClient.GetCurrentBranch();
        var branches = gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate();

        var stackData = stackConfig.Load();

        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            logger.NoStacksForRepository();
            return;
        }

        var stack = await inputProvider.SelectStack(logger, inputs.StackName, stacksForRemote, currentBranch, cancellationToken);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.StackName}' not found.");
        }

        var branchName = await inputProvider.SelectBranch(logger, inputs.BranchName, branches, cancellationToken);

        if (stack.AllBranchNames.Contains(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' already exists in stack '{stack.Name}'.");
        }

        if (!gitClient.DoesLocalBranchExist(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' does not exist locally.");
        }

        Branch? sourceBranch = null;

        var parentBranchName = await inputProvider.SelectParentBranch(logger, inputs.ParentBranchName, stack, cancellationToken);

        if (parentBranchName != stack.SourceBranch)
        {
            sourceBranch = stack.GetAllBranches().FirstOrDefault(b => b.Name.Equals(parentBranchName, StringComparison.OrdinalIgnoreCase));
            if (sourceBranch is null)
            {
                throw new InvalidOperationException($"Branch '{parentBranchName}' not found in stack '{stack.Name}'.");
            }
        }

        logger.AddingBranchToStack(branchName, stack.Name);

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

        logger.BranchAdded(branchName, stack.Name);
    }
}

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(Level = LogLevel.Trace, Message = "Adding branch {Branch} to stack \"{Stack}\".")]
    public static partial void AddingBranchToStack(this ILogger logger, string branch, string stack);

    [LoggerMessage(Level = LogLevel.Information, Message = "Branch {Branch} added to stack \"{Stack}\".")]
    public static partial void BranchAdded(this ILogger logger, string branch, string stack);
}