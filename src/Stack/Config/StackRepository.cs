using Stack.Git;
using Stack.Infrastructure.Settings;

namespace Stack.Config;

public interface IStackRepository
{
    List<Stack> GetStacks();

    void AddStack(Stack stack);

    void RemoveStack(Stack stack);

    void SaveChanges();

    string RemoteUri { get; }
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

    public string RemoteUri => gitClientFactory.Create(executionContext.WorkingDirectory).GetRemoteUri();

    public List<Stack> GetStacks()
    {
        if (string.IsNullOrEmpty(RemoteUri))
        {
            return [];
        }

        return stackData.Value.Stacks
            .Where(s => s.RemoteUri.Equals(RemoteUri, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public void AddStack(Stack stack)
    {
        stackData.Value.Stacks.Add(stack);
    }

    public void RemoveStack(Stack stack)
    {
        stackData.Value.Stacks.Remove(stack);
    }

    public void SaveChanges()
    {
        stackConfig.Save(stackData.Value);
    }
}
