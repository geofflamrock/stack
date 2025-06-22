namespace Stack.Commands;

public class PullRequestsCommand : GroupCommand
{
    public PullRequestsCommand() : base("pr", "Manage pull requests for a stack.")
    {
        Add(new CreatePullRequestsCommand());
        Add(new OpenPullRequestsCommand());
        Add(new SetPullRequestDescriptionCommand());
    }
}
