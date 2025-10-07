using Stack.Git;
using Stack.Infrastructure.Settings;

namespace Stack.Config;

/// <summary>
/// Provides a scoped, remote-aware view of stacks for the current git repository.
/// Automatically filters stacks to only those matching the current remote URI.
/// </summary>
public interface IStackRepository
{
    /// <summary>
    /// Gets all stacks for the current git remote.
    /// Returns empty list if no remote configured or no stacks exist.
    /// </summary>
    List<Stack> GetStacks();

    /// <summary>
    /// Gets a single stack by name (case-insensitive) for the current remote.
    /// Returns null if not found.
    /// </summary>
    /// <param name="name">The name of the stack to retrieve.</param>
    /// <returns>The stack if found, otherwise null.</returns>
    Stack? GetStack(string name);

    /// <summary>
    /// Adds a new stack to the current remote.
    /// </summary>
    /// <param name="stack">The stack to add.</param>
    void AddStack(Stack stack);

    /// <summary>
    /// Removes a stack from the current remote.
    /// </summary>
    /// <param name="stack">The stack to remove.</param>
    void RemoveStack(Stack stack);

    /// <summary>
    /// Saves all changes made to stacks back to the configuration file.
    /// Must be called explicitly to persist changes.
    /// </summary>
    void SaveChanges();

    /// <summary>
    /// Gets the remote URI this repository is scoped to.
    /// </summary>
    string RemoteUri { get; }
}

/// <summary>
/// Implementation of <see cref="IStackRepository"/> that filters stacks by the current git remote URI.
/// This is a scoped service that captures the remote URI at construction time.
/// </summary>
public class StackRepository : IStackRepository
{
    private readonly IStackConfig stackConfig;
    private readonly IGitClient gitClient;
    private readonly string remoteUri;
    private StackData stackData;

    public StackRepository(
        IStackConfig stackConfig,
        IGitClientFactory gitClientFactory,
        CliExecutionContext executionContext)
    {
        this.stackConfig = stackConfig;
        this.gitClient = gitClientFactory.Create(executionContext.WorkingDirectory);
        this.remoteUri = gitClient.GetRemoteUri() ?? string.Empty;
        this.stackData = stackConfig.Load();
    }

    public string RemoteUri => remoteUri;

    public List<Stack> GetStacks()
    {
        if (string.IsNullOrEmpty(remoteUri))
        {
            return [];
        }

        return stackData.Stacks
            .Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public Stack? GetStack(string name)
    {
        return GetStacks()
            .FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public void AddStack(Stack stack)
    {
        stackData.Stacks.Add(stack);
    }

    public void RemoveStack(Stack stack)
    {
        stackData.Stacks.Remove(stack);
    }

    public void SaveChanges()
    {
        stackConfig.Save(stackData);
    }
}
