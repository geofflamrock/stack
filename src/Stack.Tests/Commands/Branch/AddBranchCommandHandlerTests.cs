using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;
using Xunit.Abstractions;

namespace Stack.Tests.Commands.Branch;

public class AddBranchCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenNoInputsProvided_AsksForStackAndBranchAndParentBranchAndConfirms_AddsBranchToStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var firstBranch = Some.BranchName();
        var childBranch = Some.BranchName();
        var branchToAdd = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, firstBranch, childBranch, branchToAdd });
        gitClient.DoesLocalBranchExist(branchToAdd).Returns(true);
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(firstBranch).WithChildBranch(child => child.WithName(childBranch))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var handler = new AddBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(branchToAdd);
        inputProvider.Select(Questions.SelectParentBranch, Arg.Any<string[]>()).Returns(firstBranch);

        // Act
        await handler.Handle(new AddBranchCommandInputs(null, null, null));

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, sourceBranch, [new Config.Branch(firstBranch, [new Config.Branch(childBranch, []), new Config.Branch(branchToAdd, [])])]),
            new("Stack2", remoteUri, sourceBranch, [])
        });
    }

    [Fact]
    public async Task WhenStackNameProvided_DoesNotAskForStackName_AddsBranchFromStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var branchToAdd = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .WithStack(stack => stack.WithName("Stack2").WithRemoteUri(remoteUri).WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, anotherBranch, branchToAdd });
        gitClient.DoesLocalBranchExist(branchToAdd).Returns(true);
        var handler = new AddBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(branchToAdd);
        inputProvider.Select(Questions.SelectParentBranch, Arg.Any<string[]>()).Returns(anotherBranch);

        // Act
        await handler.Handle(new AddBranchCommandInputs("Stack1", null, null));

        // Assert
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, sourceBranch, [new Config.Branch(anotherBranch, [new Config.Branch(branchToAdd, [])])]),
            new("Stack2", remoteUri, sourceBranch, [])
        });
    }

    [Fact]
    public async Task WhenOnlyOneStackExists_DoesNotAskForStackName_AddsBranchFromStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var branchToAdd = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, anotherBranch, branchToAdd });
        gitClient.DoesLocalBranchExist(branchToAdd).Returns(true);
        var handler = new AddBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(branchToAdd);
        inputProvider.Select(Questions.SelectParentBranch, Arg.Any<string[]>()).Returns(anotherBranch);

        // Act
        await handler.Handle(new AddBranchCommandInputs(null, null, null));

        // Assert
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, sourceBranch, [new Config.Branch(anotherBranch, [new Config.Branch(branchToAdd, [])])])
        });
    }

    [Fact]
    public async Task WhenStackNameProvided_ButStackDoesNotExist_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var branchToAdd = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .WithStack(stack => stack.WithName("Stack2").WithRemoteUri(remoteUri).WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, anotherBranch, branchToAdd });
        gitClient.DoesLocalBranchExist(branchToAdd).Returns(true);
        var handler = new AddBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new AddBranchCommandInputs(invalidStackName, null, null)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }

    [Fact]
    public async Task WhenBranchNameProvided_DoesNotAskForBranchName_AddsBranchFromStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var branchToAdd = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .WithStack(stack => stack.WithName("Stack2").WithRemoteUri(remoteUri).WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, anotherBranch, branchToAdd });
        gitClient.DoesLocalBranchExist(branchToAdd).Returns(true);
        var handler = new AddBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectParentBranch, Arg.Any<string[]>()).Returns(anotherBranch);

        // Act
        await handler.Handle(new AddBranchCommandInputs(null, branchToAdd, null));

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, sourceBranch, [new Config.Branch(anotherBranch, [new Config.Branch(branchToAdd, [])])]),
            new("Stack2", remoteUri, sourceBranch, [])
        });
        inputProvider.DidNotReceive().Select(Questions.SelectBranch, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenBranchNameProvided_ButBranchDoesNotExistLocally_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var branchToAdd = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .WithStack(stack => stack.WithName("Stack2").WithRemoteUri(remoteUri).WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, anotherBranch, branchToAdd });
        gitClient.DoesLocalBranchExist(branchToAdd).Returns(true);
        var handler = new AddBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act and assert
        var invalidBranchName = Some.BranchName();
        gitClient.DoesLocalBranchExist(invalidBranchName).Returns(false);
        await handler.Invoking(async h => await h.Handle(new AddBranchCommandInputs(null, invalidBranchName, null)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Branch '{invalidBranchName}' does not exist locally.");
    }

    [Fact]
    public async Task WhenBranchNameProvided_ButBranchAlreadyExistsInStack_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var branchToAdd = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch
                    .WithName(anotherBranch)
                    .WithChildBranch(child => child.WithName(branchToAdd))))
            .WithStack(stack => stack.WithName("Stack2").WithRemoteUri(remoteUri).WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, anotherBranch, branchToAdd });
        gitClient.DoesLocalBranchExist(branchToAdd).Returns(true);
        var handler = new AddBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act and assert
        await handler.Invoking(async h => await h.Handle(new AddBranchCommandInputs(null, branchToAdd, null)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Branch '{branchToAdd}' already exists in stack 'Stack1'.");
    }

    [Fact]
    public async Task WhenAllInputsProvided_DoesNotAskForAnything_AddsBranchFromStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var branchToAdd = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .WithStack(stack => stack.WithName("Stack2").WithRemoteUri(remoteUri).WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, anotherBranch, branchToAdd });
        gitClient.DoesLocalBranchExist(branchToAdd).Returns(true);
        var handler = new AddBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        // Act
        await handler.Handle(new AddBranchCommandInputs("Stack1", branchToAdd, anotherBranch));

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, sourceBranch, [new Config.Branch(anotherBranch, [new Config.Branch(branchToAdd, [])])]),
            new("Stack2", remoteUri, sourceBranch, [])
        });
        inputProvider.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task WhenParentBranchProvided_DoesNotAskForParentBranch_CreatesNewBranchUnderneathParent()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var firstBranch = Some.BranchName();
        var childBranch = Some.BranchName();
        var branchToAdd = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, firstBranch, childBranch, branchToAdd });
        gitClient.DoesLocalBranchExist(branchToAdd).Returns(true);
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(firstBranch).WithChildBranch(child => child.WithName(childBranch))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var handler = new AddBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(branchToAdd);

        // Act
        await handler.Handle(new AddBranchCommandInputs(null, null, firstBranch));

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, sourceBranch, [new Config.Branch(firstBranch, [new Config.Branch(childBranch, []), new Config.Branch(branchToAdd, [])])]),
            new("Stack2", remoteUri, sourceBranch, [])
        });

        inputProvider.DidNotReceive().Select(Questions.SelectParentBranch, Arg.Any<string[]>());
    }
}
