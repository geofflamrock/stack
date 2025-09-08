using System.CommandLine;
using System.CommandLine.Help;

namespace Stack.Commands;

public class StackRootCommand : RootCommand
{
    public StackRootCommand(
        BranchCommand branchCommand,
        CleanupStackCommand cleanupStackCommand,
        ConfigCommand configCommand,
        DeleteStackCommand deleteStackCommand,
        ListStacksCommand listStacksCommand,
        NewStackCommand newStackCommand,
        PullRequestsCommand pullRequestsCommand,
        PullStackCommand pullStackCommand,
        PushStackCommand pushStackCommand,
        RenameStackCommand renameStackCommand,
        StackStatusCommand stackStatusCommand,
        StackSwitchCommand stackSwitchCommand,
        SyncStackCommand syncStackCommand,
        UpdateStackCommand updateStackCommand) : base("A tool to help manage multiple Git branches that stack on top of each other.")
    {
        Add(branchCommand);
        Add(cleanupStackCommand);
        Add(configCommand);
        Add(deleteStackCommand);
        Add(listStacksCommand);
        Add(newStackCommand);
        Add(pullRequestsCommand);
        Add(pullStackCommand);
        Add(pushStackCommand);
        Add(renameStackCommand);
        Add(stackStatusCommand);
        Add(stackSwitchCommand);
        Add(syncStackCommand);
        Add(updateStackCommand);

        SetAction(async (parseResult, cancellationToken) =>
        {
            await Task.CompletedTask;
            new HelpAction().Invoke(parseResult);
        });
    }
}
