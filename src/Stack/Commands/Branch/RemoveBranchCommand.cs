using System.CommandLine;

using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Commands;

public class RemoveBranchCommand : Command
{
    static readonly Option<bool> RemoveChildren = new("--remove-children")
    {
        Description = "Remove children branches."
    };

    static readonly Option<bool> MoveChildrenToParent = new("--move-children-to-parent")
    {
        Description = "Move children branches to the parent branch."
    };

    private readonly RemoveBranchCommandHandler handler;

    public RemoveBranchCommand(
        RemoveBranchCommandHandler handler,
        CliExecutionContext executionContext,
        IInputProvider inputProvider,
        IOutputProvider outputProvider,
        ILogger<RemoveBranchCommand> logger)
        : base("remove", "Remove a branch from a stack.", executionContext, inputProvider, outputProvider, logger)
    {
        this.handler = handler;
        Add(CommonOptions.Stack);
        Add(CommonOptions.Branch);
        Add(CommonOptions.Confirm);
        Add(RemoveChildren);
        Add(MoveChildrenToParent);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var removeChildren = parseResult.GetValue(RemoveChildren);
        var moveChildrenToParent = parseResult.GetValue(MoveChildrenToParent);

        if (removeChildren && moveChildrenToParent)
        {
            throw new InvalidOperationException("Cannot specify both --remove-children and --move-children-to-parent options.");
        }

        await handler.Handle(
            new RemoveBranchCommandInputs(
                parseResult.GetValue(CommonOptions.Stack),
                parseResult.GetValue(CommonOptions.Branch),
                parseResult.GetValue(CommonOptions.Confirm),
                removeChildren ? RemoveBranchChildAction.RemoveChildren : moveChildrenToParent ? RemoveBranchChildAction.MoveChildrenToParent : null),
            cancellationToken);
    }
}

public record RemoveBranchCommandInputs(string? StackName, string? BranchName, bool Confirm, RemoveBranchChildAction? RemoveChildrenAction = null)
{
    public static RemoveBranchCommandInputs Empty => new(null, null, false, null);
}

public class RemoveBranchCommandHandler(
    IInputProvider inputProvider,
    ILogger<RemoveBranchCommandHandler> logger,
    IGitClientFactory gitClientFactory,
    CliExecutionContext executionContext,
    IStackConfig stackConfig)
    : CommandHandlerBase<RemoveBranchCommandInputs>
{
    public override async Task Handle(RemoveBranchCommandInputs inputs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var stackData = stackConfig.Load();

        var gitClient = gitClientFactory.Create(executionContext.WorkingDirectory);
        var remoteUri = gitClient.GetRemoteUri();
        var currentBranch = gitClient.GetCurrentBranch();

        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();
        var stack = await inputProvider.SelectStack(logger, inputs.StackName, stacksForRemote, currentBranch, cancellationToken);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.StackName}' not found.");
        }

        var branchName = await inputProvider.SelectBranch(logger, inputs.BranchName, [.. stack.AllBranchNames], cancellationToken);

        if (!stack.AllBranchNames.Contains(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' not found in stack '{stack.Name}'.");
        }

        // Find the branch to check if it has children
        var branch = stack.FindBranch(branchName);
        var hasChildren = branch?.Children.Count > 0;

        RemoveBranchChildAction action;
        
        if (inputs.RemoveChildrenAction.HasValue)
        {
            // Use the explicitly provided action
            action = inputs.RemoveChildrenAction.Value;
        }
        else if (hasChildren)
        {
            // Only ask for action if the branch has children
            action = await inputProvider.Select(
                Questions.RemoveBranchChildAction,
                new[] { RemoveBranchChildAction.MoveChildrenToParent, RemoveBranchChildAction.RemoveChildren },
                cancellationToken,
                (action) => action.Humanize());
        }
        else
        {
            // Default action when no children (doesn't matter which one we choose)
            action = RemoveBranchChildAction.RemoveChildren;
        }

        if (inputs.Confirm || await inputProvider.Confirm(Questions.ConfirmRemoveBranch, cancellationToken))
        {
            stack.RemoveBranch(branchName, action);
            stackConfig.Save(stackData);

            logger.BranchRemovedFromStack(branchName, stack.Name);
        }
    }
}

public enum RemoveBranchChildAction
{
    [Description("Move children branches to parent branch")]
    MoveChildrenToParent,

    [Description("Remove children branches")]
    RemoveChildren
}

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Branch {Branch} removed from stack \"{Stack}\"")]
    public static partial void BranchRemovedFromStack(this ILogger logger, string branch, string stack);
}

