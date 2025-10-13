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
    private readonly IStackConfig stackConfig;
    private readonly IGitClientFactory gitClientFactory;
    private readonly CliExecutionContext executionContext;
    private readonly Lazy<StackData> stackData;

    public StackRepository(
        IStackConfig stackConfig,
        IGitClientFactory gitClientFactory,
        CliExecutionContext executionContext)
    {
        this.stackConfig = stackConfig;
        this.gitClientFactory = gitClientFactory;
        this.executionContext = executionContext;
        this.stackData = new Lazy<StackData>(() => stackConfig.Load());
    }

    public List<Model.Stack> GetStacks()
    {
        var remoteUri = gitClientFactory.Create(executionContext.WorkingDirectory).GetRemoteUri();

        return [.. stackData.Value.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase))];
    }

    public void AddStack(Model.Stack stack)
    {
        stackData.Value.Stacks.Add(stack);
    }

    public void RemoveStack(Model.Stack stack)
    {
        stackData.Value.Stacks.Remove(stack);
    }

    public void SaveChanges()
    {
        stackConfig.Save(stackData.Value);
    }
}
