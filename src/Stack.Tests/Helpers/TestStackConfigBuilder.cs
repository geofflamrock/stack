using System;
using NSubstitute;
using Stack.Config;

namespace Stack.Tests.Helpers;

public class TestStackConfigBuilder
{
    readonly List<Action<TestStackBuilder>> stackBuilders = [];

    public TestStackConfigBuilder WithStack(Action<TestStackBuilder> stackBuilder)
    {
        stackBuilders.Add(stackBuilder);
        return this;
    }

    public TestStackConfig Build()
    {
        return new TestStackConfig(
            new StackData([.. stackBuilders.Select(builder =>
            {
                var stackBuilder = new TestStackBuilder();
                builder(stackBuilder);
                return stackBuilder.Build();
            })]));
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