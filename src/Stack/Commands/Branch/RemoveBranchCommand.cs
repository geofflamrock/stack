using System.CommandLine;

using System.ComponentModel;
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
        IStdOutLogger stdOutLogger,
        IStdErrLogger stdErrLogger,
        IInputProvider inputProvider,
        CliExecutionContext executionContext,
        RemoveBranchCommandHandler handler)
    : base("remove", "Remove a branch from a stack.", stdOutLogger, stdErrLogger, inputProvider, executionContext)
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

        await handler.Handle(new RemoveBranchCommandInputs(
            parseResult.GetValue(CommonOptions.Stack),
            parseResult.GetValue(CommonOptions.Branch),
            parseResult.GetValue(CommonOptions.Confirm),
            removeChildren ? RemoveBranchChildAction.RemoveChildren : moveChildrenToParent ? RemoveBranchChildAction.MoveChildrenToParent : null));
    }
}

public record RemoveBranchCommandInputs(string? StackName, string? BranchName, bool Confirm, RemoveBranchChildAction? RemoveChildrenAction = null)
{
    public static RemoveBranchCommandInputs Empty => new(null, null, false, null);
}

public class RemoveBranchCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
    IGitClient gitClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<RemoveBranchCommandInputs>
{
    public override async Task Handle(RemoveBranchCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stackData = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();
        var currentBranch = gitClient.GetCurrentBranch();

        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();
        var stack = inputProvider.SelectStack(logger, inputs.StackName, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.StackName}' not found.");
        }

        var branchName = inputProvider.SelectBranch(logger, inputs.BranchName, [.. stack.AllBranchNames]);

        if (!stack.AllBranchNames.Contains(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' not found in stack '{stack.Name}'.");
        }

        var action =
            inputs.RemoveChildrenAction ??
            inputProvider.Select(
                Questions.RemoveBranchChildAction,
                [RemoveBranchChildAction.MoveChildrenToParent, RemoveBranchChildAction.RemoveChildren],
                (action) => action.Humanize());

        if (inputs.Confirm || inputProvider.Confirm(Questions.ConfirmRemoveBranch))
        {
            stack.RemoveBranch(branchName, action);
            stackConfig.Save(stackData);

            logger.Information($"Branch {branchName.Branch()} removed from stack {stack.Name.Stack()}");
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

