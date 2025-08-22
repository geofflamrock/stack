namespace Stack.Commands;

public class PullRequestsCommand : GroupCommand
{
    public PullRequestsCommand(
        CreatePullRequestsCommand createPullRequestsCommand,
        OpenPullRequestsCommand openPullRequestsCommand) : base("pr", "Manage pull requests for a stack.")
    {
        Add(createPullRequestsCommand);
        Add(openPullRequestsCommand);
    }
}
