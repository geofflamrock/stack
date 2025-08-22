using Microsoft.Extensions.DependencyInjection;

namespace Stack.Commands;

public class PullRequestsCommand : GroupCommand
{
    public PullRequestsCommand(IServiceProvider serviceProvider) : base("pr", "Manage pull requests for a stack.", serviceProvider)
    {
        Add(serviceProvider.GetRequiredService<CreatePullRequestsCommand>());
        Add(serviceProvider.GetRequiredService<OpenPullRequestsCommand>());
    }
}
