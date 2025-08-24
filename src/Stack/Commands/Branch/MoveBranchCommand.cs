using System.CommandLine;

using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Commands;

public class MoveBranchCommand : Command
{
    static readonly Option<bool> KeepChildrenWithOldParent = new("--keep-children-with-old-parent")
    {
        Description = "Keep children branches with the existing parent.",
    };

    static readonly Option<bool> MoveChildrenWithBranch = new("--move-children-with-branch")
    {
        Description = "Move children branches along with the branch to the new parent.",
    };

    private readonly MoveBranchCommandHandler handler;

    public MoveBranchCommand(
        IStdOutLogger stdOutLogger,
        IStdErrLogger stdErrLogger,
        IInputProvider inputProvider,
        CliExecutionContext executionContext,
        MoveBranchCommandHandler handler)
        : base("move", "Move an existing branch to a different parent within a stack.", stdOutLogger, stdErrLogger, inputProvider, executionContext)
    {
        this.handler = handler;
        Add(CommonOptions.Stack);
        Add(CommonOptions.Branch);
        Add(CommonOptions.ParentBranch);
        Add(KeepChildrenWithOldParent);
        Add(MoveChildrenWithBranch);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var keepChildren = parseResult.GetValue(KeepChildrenWithOldParent);
        var moveChildren = parseResult.GetValue(MoveChildrenWithBranch);

        if (keepChildren && moveChildren)
            throw new InvalidOperationException("Cannot specify both --keep-children-with-old-parent and --move-children-with-branch options.");

        await handler.Handle(new MoveBranchCommandInputs(
            parseResult.GetValue(CommonOptions.Stack),
            parseResult.GetValue(CommonOptions.Branch),
            parseResult.GetValue(CommonOptions.ParentBranch),
            keepChildren ? Stack.Config.MoveBranchChildrenAction.KeepChildrenWithOldParent : moveChildren ? Stack.Config.MoveBranchChildrenAction.MoveChildrenWithBranch : null));
    }
}

public record MoveBranchCommandInputs(string? StackName, string? BranchName, string? NewParentBranchName, Stack.Config.MoveBranchChildrenAction? ChildrenAction)
{
    public static MoveBranchCommandInputs Empty => new(null, null, null, null);
}

public class MoveBranchCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
    IGitClient gitClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<MoveBranchCommandInputs>
{
    public override async Task Handle(MoveBranchCommandInputs inputs)
    {
        await Task.CompletedTask;

        var stackData = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();
        var currentBranch = gitClient.GetCurrentBranch();

        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();
        var stack = inputProvider.SelectStack(logger, inputs.StackName, stacksForRemote, currentBranch)
            ?? throw new InvalidOperationException($"Stack '{inputs.StackName}' not found.");

        var branchName = inputProvider.SelectBranch(logger, inputs.BranchName, [.. stack.AllBranchNames]);

        if (!stack.AllBranchNames.Contains(branchName))
            throw new InvalidOperationException($"Branch '{branchName}' not found in stack '{stack.Name}'.");

        var newParentBranchName = inputProvider.SelectParentBranch(logger, inputs.NewParentBranchName, stack);

        var action = inputs.ChildrenAction ?? inputProvider.Select(
            Questions.MoveBranchChildrenAction,
            [Stack.Config.MoveBranchChildrenAction.KeepChildrenWithOldParent, Stack.Config.MoveBranchChildrenAction.MoveChildrenWithBranch],
            a => a.Humanize());

        logger.Information($"Moving branch {branchName.Branch()} under {newParentBranchName.Branch()} in stack {stack.Name.Stack()}");

        stack.MoveBranch(branchName, newParentBranchName, action);

        stackConfig.Save(stackData);

        logger.Information($"Branch moved");
        logger.Information($"Run {$"stack update --stack \"{stack.Name}\"".Example()} to update the stack locally, or {$"stack sync --stack \"{stack.Name}\"".Example()} to sync with the remote.");
    }
}
