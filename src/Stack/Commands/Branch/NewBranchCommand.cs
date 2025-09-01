using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using MoreLinq;
using MoreLinq.Extensions;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;
using Stack.Model;

namespace Stack.Commands;

public class NewBranchCommand : Command
{
    private readonly NewBranchCommandHandler handler;

    public NewBranchCommand(
        ILogger<NewBranchCommand> logger,
        IDisplayProvider displayProvider,
        IInputProvider inputProvider,
        CliExecutionContext executionContext,
        NewBranchCommandHandler handler)
        : base("new", "Create a new branch in a stack.", logger, displayProvider, inputProvider, executionContext)
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
    IGitClient gitClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<NewBranchCommandInputs>
{
    public override async Task Handle(NewBranchCommandInputs inputs, CancellationToken cancellationToken)
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

        logger.CreatingBranch(BranchName.From(branchName), BranchName.From(sourceBranchName), stack.Name);

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

        logger.BranchCreated(BranchName.From(branchName));

        try
        {
            logger.PushingBranch(BranchName.From(branchName));
            gitClient.PushNewBranch(branchName);
        }
        catch (Exception)
        {
            logger.NewBranchPushWarning(BranchName.From(branchName), Example.From($"stack push --name \"{stack.Name}\""));
        }

        gitClient.ChangeBranch(branchName);
    }
}

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Pushing branch {BranchName} to remote repository.")]
    [SuppressMessage("LoggerMessage", "LOGGEN036:A value being logged doesn't have an effective way to be converted into a string", Justification = "Types are generated and have a ToString() method")]
    public static partial void PushingBranch(this ILogger logger, BranchName branchName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Creating branch {BranchName} from {SourceBranchName} in stack {StackName}.")]
    [SuppressMessage("LoggerMessage", "LOGGEN036:A value being logged doesn't have an effective way to be converted into a string", Justification = "Types are generated and have a ToString() method")]
    public static partial void CreatingBranch(this ILogger logger, BranchName branchName, BranchName sourceBranchName, StackName stackName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Branch {BranchName} created.")]
    [SuppressMessage("LoggerMessage", "LOGGEN036:A value being logged doesn't have an effective way to be converted into a string", Justification = "Types are generated and have a ToString() method")]
    public static partial void BranchCreated(this ILogger logger, BranchName branchName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "An error has occurred pushing branch {BranchName} to remote repository. Use `{Example}` to push the branch to the remote repository.")]
    [SuppressMessage("LoggerMessage", "LOGGEN036:A value being logged doesn't have an effective way to be converted into a string", Justification = "Types are generated and have a ToString() method")]
    public static partial void NewBranchPushWarning(this ILogger logger, BranchName branchName, Example example);
}