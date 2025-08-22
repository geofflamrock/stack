using System.CommandLine;
using System.CommandLine.Help;
using Microsoft.Extensions.DependencyInjection;

namespace Stack.Commands;

public class StackRootCommand : RootCommand
{
    public StackRootCommand(IServiceProvider serviceProvider) : base("A tool to help manage multiple Git branches that stack on top of each other.")
    {
        Add(serviceProvider.GetRequiredService<BranchCommand>());
        Add(serviceProvider.GetRequiredService<CleanupStackCommand>());
        Add(serviceProvider.GetRequiredService<ConfigCommand>());
        Add(serviceProvider.GetRequiredService<DeleteStackCommand>());
        Add(serviceProvider.GetRequiredService<ListStacksCommand>());
        Add(serviceProvider.GetRequiredService<NewStackCommand>());
        Add(serviceProvider.GetRequiredService<PullRequestsCommand>());
        Add(serviceProvider.GetRequiredService<PullStackCommand>());
        Add(serviceProvider.GetRequiredService<PushStackCommand>());
        Add(serviceProvider.GetRequiredService<StackStatusCommand>());
        Add(serviceProvider.GetRequiredService<StackSwitchCommand>());
        Add(serviceProvider.GetRequiredService<SyncStackCommand>());
        Add(serviceProvider.GetRequiredService<UpdateStackCommand>());

        SetAction(async (parseResult, cancellationToken) =>
        {
            await Task.CompletedTask;
            new HelpAction().Invoke(parseResult);
        });
    }
}
