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
    private readonly Lazy<List<Model.Stack>> allStacks;

    public StackRepository(
        IStackDataStore dataStore,
        IGitClientFactory gitClientFactory,
        CliExecutionContext executionContext)
    {
        this.dataStore = dataStore;
        this.gitClientFactory = gitClientFactory;
        this.executionContext = executionContext;
        allStacks = new Lazy<List<Model.Stack>>(() => LoadData());
    }

    public List<Model.Stack> GetStacks()
    {
        return allStacks.Value;
    }

    public void AddStack(Model.Stack stack)
    {
        allStacks.Value.Add(stack);
    }

    public void RemoveStack(Model.Stack stack)
    {
        var remoteUri = GetRemoteUri();
        var stackToRemove = allStacks.Value.FirstOrDefault(s => s.Name == stack.Name);

        if (stackToRemove == null)
        {
            throw new InvalidOperationException($"Stack '{stack.Name}' does not exist in the current repository.");
        }

        allStacks.Value.Remove(stackToRemove);
    }

    public void SaveChanges()
    {
        var remoteUri = GetRemoteUri();
        var stackData = dataStore.Load();
        stackData.Stacks.RemoveAll(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase));
        stackData.Stacks.AddRange(allStacks.Value.Select(s => new StackDataItem(s.Name, remoteUri, s.SourceBranch, [.. s.Branches.Select(b => MapToDataBranch(b))])));
        dataStore.Save(stackData);
    }

    private List<Model.Stack> LoadData()
    {
        var remoteUri = GetRemoteUri();

        var stackData = dataStore.Load();
        return [.. stackData.Stacks
            .Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase))
            .Select(s => new Model.Stack(s.Name, s.SourceBranch, [.. s.Branches.Select(b => MapToModelBranch(b))]))];
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
