using FluentAssertions;
using Meziantou.Extensions.Logging.Xunit;
using NSubstitute;
using Stack.Commands.Helpers;
using Stack.Infrastructure;
using Stack.Tests.Helpers;
using Xunit.Abstractions;

namespace Stack.Tests.Commands.Helpers;

public class InputProviderExtensionMethodsTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task SelectStack_WhenNameIsProvided_ReturnsStackByName()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var currentBranch = Some.BranchName();
        var stackName = "TestStack";

        var stacks = new List<Model.Stack>
        {
            new(stackName, sourceBranch, []),
            new("OtherStack", sourceBranch, [])
        };

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<InputProviderExtensionMethodsTests>(testOutputHelper);

        // Act
        var result = await inputProvider.SelectStack(logger, stackName, stacks, currentBranch, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be(stackName);
        await inputProvider.DidNotReceive().Select(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectStack_WhenOnlyOneStackExists_AutoSelectsStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var currentBranch = Some.BranchName();
        var stackName = "OnlyStack";

        var stacks = new List<Model.Stack>
        {
            new(stackName, sourceBranch, [])
        };

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<InputProviderExtensionMethodsTests>(testOutputHelper);

        // Act
        var result = await inputProvider.SelectStack(logger, null, stacks, currentBranch, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be(stackName);
        await inputProvider.DidNotReceive().Select(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectStack_WhenCurrentBranchIsInOnlyOneStack_AutoSelectsThatStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var currentBranch = Some.BranchName();
        var stackWithCurrentBranch = "StackWithBranch";
        var stackWithoutCurrentBranch = "StackWithoutBranch";

        var stacks = new List<Model.Stack>
        {
            new(stackWithCurrentBranch, sourceBranch, [new Model.Branch(currentBranch, [])]),
            new(stackWithoutCurrentBranch, sourceBranch, [new Model.Branch(Some.BranchName(), [])])
        };

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<InputProviderExtensionMethodsTests>(testOutputHelper);

        // Act
        var result = await inputProvider.SelectStack(logger, null, stacks, currentBranch, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be(stackWithCurrentBranch);
        await inputProvider.DidNotReceive().Select(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectStack_WhenCurrentBranchIsInMultipleStacks_PromptsUser()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var currentBranch = Some.BranchName();
        var stack1Name = "Stack1";
        var stack2Name = "Stack2";

        var stacks = new List<Model.Stack>
        {
            new(stack1Name, sourceBranch, [new Model.Branch(currentBranch, [])]),
            new(stack2Name, sourceBranch, [new Model.Branch(currentBranch, [])])
        };

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<InputProviderExtensionMethodsTests>(testOutputHelper);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(stack1Name);

        // Act
        var result = await inputProvider.SelectStack(logger, null, stacks, currentBranch, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be(stack1Name);
        await inputProvider.Received(1).Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectStack_WhenCurrentBranchIsInNoStacks_PromptsUser()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var currentBranch = Some.BranchName();
        var stack1Name = "Stack1";
        var stack2Name = "Stack2";

        var stacks = new List<Model.Stack>
        {
            new(stack1Name, sourceBranch, [new Model.Branch(Some.BranchName(), [])]),
            new(stack2Name, sourceBranch, [new Model.Branch(Some.BranchName(), [])])
        };

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<InputProviderExtensionMethodsTests>(testOutputHelper);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(stack1Name);

        // Act
        var result = await inputProvider.SelectStack(logger, null, stacks, currentBranch, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be(stack1Name);
        await inputProvider.Received(1).Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectStack_WhenCurrentBranchIsSourceBranchInOnlyOneStack_AutoSelectsThatStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var currentBranch = sourceBranch; // Current branch is the source branch
        var stackWithCurrentAsSource = "StackWithSourceBranch";
        var stackWithDifferentSource = "StackWithDifferentSource";

        var stacks = new List<Model.Stack>
        {
            new(stackWithCurrentAsSource, sourceBranch, []),
            new(stackWithDifferentSource, Some.BranchName(), [])
        };

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<InputProviderExtensionMethodsTests>(testOutputHelper);

        // Act
        var result = await inputProvider.SelectStack(logger, null, stacks, currentBranch, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be(stackWithCurrentAsSource);
        await inputProvider.DidNotReceive().Select(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }
}