using Stack.Model;
using Stack.Persistence;

namespace Stack.Tests.Helpers;

public class TestStackRepositoryBuilder
{
    readonly List<Action<TestStackBuilder>> stackBuilders = [];

    public TestStackRepositoryBuilder WithStack(Action<TestStackBuilder> stackBuilder)
    {
        stackBuilders.Add(stackBuilder);
        return this;
    }

    public TestStackRepository Build()
    {
        List<Model.Stack> stacks = [.. stackBuilders.Select(builder =>
        {
            var stackBuilder = new TestStackBuilder();
            builder(stackBuilder);
            return stackBuilder.Build();
        })];

        return new TestStackRepository(stacks);
    }
}

public class TestStackBuilder
{
    string? name;
    string? sourceBranch;
    List<Action<TestStackBranchBuilder>> branchBuilders = [];

    public TestStackBuilder WithName(string name)
    {
        this.name = name;
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

    public Model.Stack Build()
    {
        var branches = branchBuilders
            .Select(builder =>
            {
                var branchBuilder = new TestStackBranchBuilder();
                builder(branchBuilder);
                return branchBuilder.Build();
            })
            .ToList();

        var stack = new Model.Stack(name ?? Some.Name(), sourceBranch ?? Some.BranchName(), branches);

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

public class TestStackRepository : IStackRepository
{
    private readonly List<Model.Stack> stacks;
    private readonly string remoteUri = Some.HttpsUri().ToString();

    public TestStackRepository(List<Model.Stack> initialData)
    {
        this.stacks = initialData;
    }

    public string RemoteUri => remoteUri;

    public List<Model.Stack> GetStacks()
    {
        if (string.IsNullOrEmpty(remoteUri))
        {
            return [];
        }

        return stacks.ToList();
    }

    public void AddStack(Model.Stack stack)
    {
        stacks.Add(stack);
    }

    public void RemoveStack(Model.Stack stack)
    {
        stacks.Remove(stack);
    }

    public void SaveChanges()
    {
        // No-op for testing - changes are already in memory
    }

    public List<Model.Stack> Stacks => stacks;
}
