using System.CommandLine;
using Microsoft.Extensions.Logging;
using MoreLinq;
using MoreLinq.Extensions;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Commands;

public class NewBranchCommand : Command
{
    private readonly NewBranchCommandHandler handler;

    public NewBranchCommand(
        NewBranchCommandHandler handler,
        CliExecutionContext executionContext,
        IInputProvider inputProvider,
        IOutputProvider outputProvider,
        ILogger<NewBranchCommand> logger)
        : base("new", "Create a new branch in a stack.", executionContext, inputProvider, outputProvider, logger)
    {
        this.handler = handler;
        Add(CommonOptions.Stack);
        Add(CommonOptions.Branch);
        Add(CommonOptions.ParentBranch);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await handler.Handle(
            new NewBranchCommandInputs(
                parseResult.GetValue(CommonOptions.Stack),
                parseResult.GetValue(CommonOptions.Branch),
                parseResult.GetValue(CommonOptions.ParentBranch)),
            cancellationToken);
    }
}

public record NewBranchCommandInputs(string? StackName, string? BranchName, string? ParentBranchName)
{
    public static NewBranchCommandInputs Empty => new(null, null, null);
}

public class NewBranchCommandHandler(
    IInputProvider inputProvider,
    ILogger<NewBranchCommandHandler> logger,
    IDisplayProvider displayProvider,
    IGitClientFactory gitClientFactory,
    CliExecutionContext executionContext,
    IStackRepository repository)
    : CommandHandlerBase<NewBranchCommandInputs>
{
    public override async Task Handle(NewBranchCommandInputs inputs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var gitClient = gitClientFactory.Create(executionContext.WorkingDirectory);
        var currentBranch = gitClient.GetCurrentBranch();
        var branches = gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate();

        var stacksForRemote = repository.GetStacks();

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

        var branchName = await inputProvider.Text(logger, Questions.BranchName, inputs.BranchName, cancellationToken);

        if (stack.AllBranchNames.Contains(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' already exists in stack '{stack.Name}'.");
        }

        if (gitClient.DoesLocalBranchExist(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' already exists locally.");
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

        var sourceBranchName = sourceBranch?.Name ?? stack.SourceBranch;

        logger.CreatingBranch(branchName, sourceBranchName, stack.Name);

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

        repository.SaveChanges();

        try
        {
            await displayProvider.DisplayStatus($"Pushing branch '{branchName}' to remote repository...", async (ct) =>
            {
                await Task.CompletedTask;
                gitClient.PushNewBranch(branchName);
            }, cancellationToken);
        }
        catch (Exception)
        {
            logger.NewBranchPushWarning(branchName, stack.Name);
        }

        gitClient.ChangeBranch(branchName);

        logger.BranchCreated(branchName, stack.Name);
    }
}

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating branch {Branch} from {SourceBranch} in stack \"{Stack}\"")]
    public static partial void CreatingBranch(this ILogger logger, string branch, string sourceBranch, string stack);

    [LoggerMessage(Level = LogLevel.Information, Message = "Branch {Branch} created in stack \"{Stack}\".")]
    public static partial void BranchCreated(this ILogger logger, string branch, string stack);
}