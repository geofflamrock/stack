using System.CommandLine;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Commands;

public enum MoveBranchChildAction
{
    [Description("Move children branches with the branch being moved")]
    MoveChildren,

    [Description("Re-parent children branches to the previous location")]
    ReParentChildren
}

public class MoveBranchCommand : Command
{
    static readonly Option<bool> ReParentChildren = new("--re-parent-children")
    {
        Description = "Re-parent children branches to the previous location."
    };

    static readonly Option<bool> MoveChildren = new("--move-children")
    {
        Description = "Move children branches with the branch being moved."
    };

    private readonly MoveBranchCommandHandler handler;

    public MoveBranchCommand(
        ILogger<MoveBranchCommand> logger,
        IDisplayProvider displayProvider,
        IInputProvider inputProvider,
        CliExecutionContext executionContext,
        MoveBranchCommandHandler handler)
        : base("move", "Move a branch to another location in a stack.", logger, displayProvider, inputProvider, executionContext)
    {
        this.handler = handler;
        Add(CommonOptions.Stack);
        Add(CommonOptions.Branch);
        Add(CommonOptions.ParentBranch);
        Add(ReParentChildren);
        Add(MoveChildren);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var reParentChildren = parseResult.GetValue(ReParentChildren);
        var moveChildren = parseResult.GetValue(MoveChildren);

        if (reParentChildren && moveChildren)
        {
            throw new InvalidOperationException("Cannot specify both --re-parent-children and --move-children options.");
        }

        await handler.Handle(
            new MoveBranchCommandInputs(
                parseResult.GetValue(CommonOptions.Stack),
                parseResult.GetValue(CommonOptions.Branch),
                parseResult.GetValue(CommonOptions.ParentBranch),
                reParentChildren ? MoveBranchChildAction.ReParentChildren : moveChildren ? MoveBranchChildAction.MoveChildren : null),
            cancellationToken);
    }
}

public record MoveBranchCommandInputs(string? StackName, string? BranchName, string? NewParentBranchName, MoveBranchChildAction? ChildAction = null)
{
    public static MoveBranchCommandInputs Empty => new(null, null, null, null);
}

public class MoveBranchCommandHandler(
    IInputProvider inputProvider,
    ILogger<MoveBranchCommandHandler> logger,
    IGitClient gitClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<MoveBranchCommandInputs>
{
    public override async Task Handle(MoveBranchCommandInputs inputs, CancellationToken cancellationToken)
    {
        var remoteUri = gitClient.GetRemoteUri();
        var currentBranch = gitClient.GetCurrentBranch();

        var stackData = stackConfig.Load();
        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            throw new InvalidOperationException("No stacks found for this repository.");
        }

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

        var newParentBranchName = await inputProvider.SelectParentBranch(logger, inputs.NewParentBranchName, stack, cancellationToken);

        // Get the branch being moved and check if it has children
        var branchBeingMoved = stack.GetAllBranches().FirstOrDefault(b => b.Name.Equals(branchName, StringComparison.OrdinalIgnoreCase));
        var hasChildren = branchBeingMoved?.Children.Count > 0;

        MoveBranchChildAction childAction = MoveBranchChildAction.MoveChildren; // default

        if (hasChildren && inputs.ChildAction is null)
        {
            childAction = await inputProvider.Select(
                Questions.MoveBranchChildAction,
                new[] { MoveBranchChildAction.MoveChildren, MoveBranchChildAction.ReParentChildren },
                cancellationToken,
                (action) => action.Humanize());
        }
        else if (inputs.ChildAction is not null)
        {
            childAction = inputs.ChildAction.Value;
        }

        stack.MoveBranch(branchName, newParentBranchName, childAction);

        stackConfig.Save(stackData);

        logger.BranchMovedInStack(branchName, stack.Name);
        logger.SuggestStackUpdate();
    }
}

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Branch {Branch} moved in stack \"{Stack}\"")]
    public static partial void BranchMovedInStack(this ILogger logger, string branch, string stack);

    [LoggerMessage(Level = LogLevel.Information, Message = "Run 'stack sync' or 'stack update' to synchronize the changes with Git.")]
    public static partial void SuggestStackUpdate(this ILogger logger);
}