using System;
using NSubstitute;
using Stack.Config;

namespace Stack.Tests.Helpers;

public class TestStackRepositoryBuilder
{
    readonly List<Action<TestStackBuilder>> stackBuilders = [];
    string remoteUri = Some.HttpsUri().ToString();

    public TestStackRepositoryBuilder WithStack(Action<TestStackBuilder> stackBuilder)
    {
        stackBuilders.Add(stackBuilder);
        return this;
    }

    public TestStackRepositoryBuilder WithRemoteUri(string uri)
    {
        remoteUri = uri;
        return this;
    }

    public TestStackRepository Build()
    {
        var stackData = new StackData([.. stackBuilders.Select(builder =>
        {
            var stackBuilder = new TestStackBuilder();
            stackBuilder = stackBuilder.WithRemoteUri(remoteUri);
            builder(stackBuilder);
            return stackBuilder.Build();
        })]);

        return new TestStackRepository(stackData, remoteUri);
    }
}

public class TestStackBuilder
{
    string? name;
    string? remoteUri;
    string? sourceBranch;
    List<Action<TestStackBranchBuilder>> branchBuilders = [];

    public TestStackBuilder WithName(string name)
    {
        this.name = name;
        return this;
    }

    public TestStackBuilder WithRemoteUri(string remoteUri)
    {
        this.remoteUri = remoteUri;
        return this;
    }

    public TestStackBuilder WithSourceBranch(string sourceBranch)
    {
        this.sourceBranch = sourceBranch;
        return this;
    }

    public TestStackBuilder WithBranch(Action<TestStackBranchBuilder> branchBuilder)
    {
        branchBuilders.Add(branchBuilder);
        return this;
    }

    public Config.Stack Build()
    {
        var branches = branchBuilders
            .Select(builder =>
            {
                var branchBuilder = new TestStackBranchBuilder();
                builder(branchBuilder);
                return branchBuilder.Build();
            })
            .ToList();

        var stack = new Config.Stack(name ?? Some.Name(), remoteUri ?? Some.HttpsUri().ToString(), sourceBranch ?? Some.BranchName(), branches);

        return stack;
    }
}

public class TestStackBranchBuilder
{
    string? name;

    List<Action<TestStackBranchBuilder>> childBranchBuilders = [];

    public TestStackBranchBuilder WithName(string name)
    {
        this.name = name;
        return this;
    }

    public TestStackBranchBuilder WithChildBranch(Action<TestStackBranchBuilder> childBranchBuilder)
    {
        childBranchBuilders.Add(childBranchBuilder);
        return this;
    }

    public Branch Build()
    {
        return new Branch(
            name ?? Some.BranchName(),
            [.. childBranchBuilders.Select(builder =>
            {
                var branchBuilder = new TestStackBranchBuilder();
                builder(branchBuilder);
                return branchBuilder.Build();
            })]);
    }
}

public class TestStackConfig(StackData initialData) : IStackConfig
{
    StackData stackData = initialData;

    public List<Config.Stack> Stacks => stackData.Stacks;

    public string GetConfigPath() => throw new NotImplementedException("TestStackConfig does not support GetConfigPath.");

    public StackData Load() => stackData;

    public void Save(StackData newStackData)
    {
        stackData = newStackData;
    }
}
public class TestStackRepository : IStackRepository
{
    private readonly StackData stackData;
    private readonly string remoteUri;

    public TestStackRepository(StackData initialData, string remoteUri)
    {
        this.stackData = initialData;
        this.remoteUri = remoteUri;
    }

    public string RemoteUri => remoteUri;

    public List<Config.Stack> GetStacks()
    {
        if (string.IsNullOrEmpty(remoteUri))
        {
            return [];
        }

        return stackData.Stacks
            .Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public Config.Stack? GetStack(string name)
    {
        return GetStacks()
            .FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public void AddStack(Config.Stack stack)
    {
        stackData.Stacks.Add(stack);
    }

    public void RemoveStack(Config.Stack stack)
    {
        stackData.Stacks.Remove(stack);
    }

    public void SaveChanges()
    {
        // No-op for testing - changes are already in memory
    }

    public List<Config.Stack> Stacks => stackData.Stacks;
}
