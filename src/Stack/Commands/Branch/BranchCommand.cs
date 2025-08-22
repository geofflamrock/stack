using Microsoft.Extensions.DependencyInjection;

namespace Stack.Commands;

public class BranchCommand : GroupCommand
{
    public BranchCommand(IServiceProvider serviceProvider) : base("branch", "Manage branches within a stack.", serviceProvider)
    {
        Add(serviceProvider.GetRequiredService<AddBranchCommand>());
        Add(serviceProvider.GetRequiredService<NewBranchCommand>());
        Add(serviceProvider.GetRequiredService<RemoveBranchCommand>());
    }
}

