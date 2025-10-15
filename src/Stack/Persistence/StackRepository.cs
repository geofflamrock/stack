using Stack.Git;
using Stack.Infrastructure.Settings;

namespace Stack.Persistence;

public interface IStackRepository
{
    List<Model.Stack> GetStacks();

    void AddStack(Model.Stack stack);

    void RemoveStack(Model.Stack stack);

    void SaveChanges();
}

public class StackRepository : IStackRepository
{
    private readonly IStackDataStore dataStore;
    private readonly IGitClientFactory gitClientFactory;
    private readonly CliExecutionContext executionContext;
    private readonly Lazy<StackData> stackData;

    public StackRepository(
        IStackDataStore dataStore,
        IGitClientFactory gitClientFactory,
        CliExecutionContext executionContext)
    {
        this.dataStore = dataStore;
        this.gitClientFactory = gitClientFactory;
        this.executionContext = executionContext;
        this.stackData = new Lazy<StackData>(() => dataStore.Load());
    }

    public List<Model.Stack> GetStacks()
    {
        var remoteUri = GetRemoteUri();

        return [.. stackData.Value.Stacks
            .Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase))
            .Select(s => new Model.Stack(
                s.Name,
                s.SourceBranch,
                [.. s.Branches.Select(b => MapToModelBranch(b))]))];
    }

    public void AddStack(Model.Stack stack)
    {
        var remoteUri = GetRemoteUri();

        stackData.Value.Stacks.Add(
            new StackDataItem(stack.Name, remoteUri, stack.SourceBranch, [.. stack.Branches.Select(b => MapToDataBranch(b))]));
    }

    public void RemoveStack(Model.Stack stack)
    {
        var remoteUri = GetRemoteUri();
        var stackToRemove = stackData.Value.Stacks.FirstOrDefault(s => s.Name == stack.Name && s.RemoteUri == remoteUri);
        if (stackToRemove == null)
        {
            throw new InvalidOperationException($"Stack '{stack.Name}' does not exist in the current repository.");
        }
        stackData.Value.Stacks.Remove(stackToRemove);
    }

    public void SaveChanges()
    {
        dataStore.Save(stackData.Value);
    }

    private string GetRemoteUri()
    {
        return gitClientFactory.Create(executionContext.WorkingDirectory).GetRemoteUri();
    }

    private static Model.Branch MapToModelBranch(StackBranchItem branchItem)
    {
        return new Model.Branch(
            branchItem.Name,
            [.. branchItem.Children.Select(b => MapToModelBranch(b))]);
    }

    private static StackBranchItem MapToDataBranch(Model.Branch branch)
    {
        return new StackBranchItem(
            branch.Name,
            [.. branch.Children.Select(b => MapToDataBranch(b))]);
    }
}
