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
    private readonly IGitClient gitClient;
    private readonly string remoteUri;
    private readonly Lazy<StackData> stackData;

    public StackRepository(
        IStackConfig stackConfig,
        IGitClientFactory gitClientFactory,
        CliExecutionContext executionContext)
    {
        this.stackConfig = stackConfig;
        this.gitClient = gitClientFactory.Create(executionContext.WorkingDirectory);
        this.remoteUri = gitClient.GetRemoteUri();
        this.stackData = new Lazy<StackData>(() => stackConfig.Load());
    }

    public string RemoteUri => remoteUri;

    public List<Stack> GetStacks()
    {
        if (string.IsNullOrEmpty(remoteUri))
        {
            return [];
        }

        return stackData.Value.Stacks
            .Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase))
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
