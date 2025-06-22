using System.CommandLine;
using System.ComponentModel;
using Humanizer;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class RemoveBranchCommand : Command
{
    public RemoveBranchCommand() : base("remove", "Remove a branch from a stack.")
    {
        Add(CommonOptions.Stack);
        Add(CommonOptions.Branch);
        Add(CommonOptions.Confirm);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var handler = new RemoveBranchCommandHandler(
            InputProvider,
            StdErrLogger,
            new GitClient(StdErrLogger, new GitClientSettings(Verbose, WorkingDirectory)),
            new FileStackConfig());

        await handler.Handle(new RemoveBranchCommandInputs(
            parseResult.GetValue(CommonOptions.Stack),
            parseResult.GetValue(CommonOptions.Branch),
            parseResult.GetValue(CommonOptions.Confirm)));
    }
}

public record RemoveBranchCommandInputs(string? StackName, string? BranchName, bool Confirm)
{
    public static RemoveBranchCommandInputs Empty => new(null, null, false);
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

        var action = RemoveBranchChildAction.MoveChildrenToParent;

        if (stackData.SchemaVersion == SchemaVersion.V2)
        {
            action = inputProvider.Select(
                Questions.RemoveBranchChildAction,
                [RemoveBranchChildAction.MoveChildrenToParent, RemoveBranchChildAction.RemoveChildren],
                (action) => action.Humanize());
        }

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

