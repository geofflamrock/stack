using System.CommandLine;
using System.CommandLine.Help;

namespace Stack.Commands;

public class StackRootCommand : RootCommand
{
    public StackRootCommand() : base("A tool to help manage multiple Git branches that stack on top of each other.")
    {
        Add(new BranchCommand());
        Add(new CleanupStackCommand());
        Add(new ConfigCommand());
        Add(new DeleteStackCommand());
        Add(new ListStacksCommand());
        Add(new NewStackCommand());
        Add(new PullRequestsCommand());
        Add(new PullStackCommand());
        Add(new PushStackCommand());
        Add(new StackStatusCommand());
        Add(new StackSwitchCommand());
        Add(new SyncStackCommand());
        Add(new UpdateStackCommand());

        SetAction(async (parseResult, cancellationToken) =>
        {
            await Task.CompletedTask;
            new HelpAction().Invoke(parseResult);
        });
    }
}
