using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Config;
using Stack.Git;
using Stack.Tests.Helpers;

namespace Stack.Tests.Commands.Stack;

public class NewStackCommandHandlerTests
{
    [Fact]
    public async Task WithANewBranch_AndSwitchingToTheBranch_TheStackIsCreatedAndTheCurrentBranchIsChanged()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<INewStackCommandInputProvider>();
        var handler = new NewStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(["branch-1", "branch-2"]);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.GetStackName().Returns("Stack1");
        inputProvider.GetSourceBranch(Arg.Any<string[]>()).Returns("branch-1");
        inputProvider.ConfirmAddOrCreateBranch().Returns(true);
        inputProvider.SelectAddOrCreateBranch().Returns(BranchAction.Create);
        inputProvider.GetNewBranchName().Returns("new-branch");
        inputProvider.ConfirmSwitchToBranch().Returns(true);

        // Act
        var response = await handler.Handle(NewStackCommandInputs.Empty);

        // Assert
        response.Should().BeEquivalentTo(new NewStackCommandResponse("Stack1", "branch-1", BranchAction.Create, "new-branch"));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["new-branch"])
        });

        gitOperations.Received().ChangeBranch("new-branch");
    }

    [Fact]
    public async Task WithAnExistingBranch_AndSwitchingToTheBranch_TheStackIsCreatedAndTheCurrentBranchIsChanged()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<INewStackCommandInputProvider>();
        var handler = new NewStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(["branch-1", "branch-2"]);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.GetStackName().Returns("Stack1");
        inputProvider.GetSourceBranch(Arg.Any<string[]>()).Returns("branch-1");
        inputProvider.ConfirmAddOrCreateBranch().Returns(true);
        inputProvider.SelectAddOrCreateBranch().Returns(BranchAction.Add);
        inputProvider.GetBranchToAdd(Arg.Any<string[]>()).Returns("branch-2");
        inputProvider.ConfirmSwitchToBranch().Returns(true);

        // Act
        var response = await handler.Handle(NewStackCommandInputs.Empty);

        // Assert
        response.Should().BeEquivalentTo(new NewStackCommandResponse("Stack1", "branch-1", BranchAction.Add, "branch-2"));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["branch-2"])
        });

        gitOperations.Received().ChangeBranch("branch-2");
    }

    [Fact]
    public async Task WithNoBranch_TheStackIsCreatedAndTheCurrentBranchIsNotChanged()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<INewStackCommandInputProvider>();
        var handler = new NewStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(["branch-1", "branch-2"]);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.GetStackName().Returns("Stack1");
        inputProvider.GetSourceBranch(Arg.Any<string[]>()).Returns("branch-1");
        inputProvider.ConfirmAddOrCreateBranch().Returns(false);

        // Act
        var response = await handler.Handle(NewStackCommandInputs.Empty);

        // Assert
        response.Should().BeEquivalentTo(new NewStackCommandResponse("Stack1", "branch-1", null, null));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, "branch-1", [])
        });

        gitOperations.DidNotReceive().ChangeBranch(Arg.Any<string>());
    }

    [Fact]
    public async Task WithANewBranch_AndNotSwitchingToTheBranch_TheStackIsCreatedAndTheCurrentBranchIsNotChanged()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<INewStackCommandInputProvider>();
        var handler = new NewStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(["branch-1", "branch-2"]);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.GetStackName().Returns("Stack1");
        inputProvider.GetSourceBranch(Arg.Any<string[]>()).Returns("branch-1");
        inputProvider.ConfirmAddOrCreateBranch().Returns(true);
        inputProvider.SelectAddOrCreateBranch().Returns(BranchAction.Create);
        inputProvider.GetNewBranchName().Returns("new-branch-1");
        inputProvider.ConfirmSwitchToBranch().Returns(false);

        // Act
        var response = await handler.Handle(NewStackCommandInputs.Empty);

        // Assert
        response.Should().BeEquivalentTo(new NewStackCommandResponse("Stack1", "branch-1", BranchAction.Create, "new-branch-1"));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["new-branch-1"])
        });

        gitOperations.DidNotReceive().ChangeBranch("new-branch-1");
    }

    [Fact]
    public async Task WithAnExistingBranch_AndNotSwitchingToTheBranch_TheStackIsCreatedAndTheCurrentBranchIsNotChanged()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<INewStackCommandInputProvider>();
        var handler = new NewStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(["branch-1", "branch-2"]);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.GetStackName().Returns("Stack1");
        inputProvider.GetSourceBranch(Arg.Any<string[]>()).Returns("branch-1");
        inputProvider.ConfirmAddOrCreateBranch().Returns(true);
        inputProvider.SelectAddOrCreateBranch().Returns(BranchAction.Add);
        inputProvider.GetBranchToAdd(Arg.Any<string[]>()).Returns("branch-2");
        inputProvider.ConfirmSwitchToBranch().Returns(false);

        // Act
        var response = await handler.Handle(NewStackCommandInputs.Empty);

        // Assert
        response.Should().BeEquivalentTo(new NewStackCommandResponse("Stack1", "branch-1", BranchAction.Add, "branch-2"));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["branch-2"])
        });

        gitOperations.DidNotReceive().ChangeBranch("branch-2");
    }

    [Fact]
    public async Task WhenStackNameIsProvidedInInputs_TheProviderIsNotAskedForAName_AndTheStackIsCreatedWithTheName()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<INewStackCommandInputProvider>();
        var handler = new NewStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(["branch-1", "branch-2"]);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.GetSourceBranch(Arg.Any<string[]>()).Returns("branch-1");
        inputProvider.ConfirmAddOrCreateBranch().Returns(false);

        var inputs = new NewStackCommandInputs("Stack1", null, null);

        // Act
        var response = await handler.Handle(inputs);

        // Assert
        response.Should().BeEquivalentTo(new NewStackCommandResponse("Stack1", "branch-1", null, null));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, "branch-1", [])
        });

        inputProvider.DidNotReceive().GetStackName();
    }

    [Fact]
    public async Task WhenSourceBranchIsProvidedInInputs_TheProviderIsNotAskedForTheBranch_AndTheStackIsCreatedWithTheSourceBranch()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<INewStackCommandInputProvider>();
        var handler = new NewStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(["branch-1", "branch-2"]);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.GetStackName().Returns("Stack1");
        inputProvider.ConfirmAddOrCreateBranch().Returns(false);

        var inputs = new NewStackCommandInputs(null, "branch-1", null);

        // Act
        var response = await handler.Handle(inputs);

        // Assert
        response.Should().BeEquivalentTo(new NewStackCommandResponse("Stack1", "branch-1", null, null));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, "branch-1", [])
        });

        inputProvider.DidNotReceive().GetSourceBranch(Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenBranchNameIsProvidedInInputs_TheProviderIsNotAskedForTheBranchName_AndTheStackIsCreatedWithTheBranchName()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<INewStackCommandInputProvider>();
        var handler = new NewStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(["branch-1", "branch-2"]);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.GetStackName().Returns("Stack1");
        inputProvider.GetSourceBranch(Arg.Any<string[]>()).Returns("branch-1");
        // Note there shouldn't be any more inputs required at all

        var inputs = new NewStackCommandInputs(null, null, "new-branch");

        // Act
        var response = await handler.Handle(inputs);

        // Assert
        response.Should().BeEquivalentTo(new NewStackCommandResponse("Stack1", "branch-1", BranchAction.Create, "new-branch"));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["new-branch"])
        });

        inputProvider.Received().GetStackName();
        inputProvider.Received().GetSourceBranch(Arg.Any<string[]>());
        inputProvider.ClearReceivedCalls();
        inputProvider.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task WhenAllInputsAreProvided_TheProviderIsNotAskedForAnything_AndTheStackIsCreatedCorrectly()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<INewStackCommandInputProvider>();
        var handler = new NewStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(["branch-1", "branch-2"]);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.GetStackName().Returns("Stack1");
        inputProvider.GetSourceBranch(Arg.Any<string[]>()).Returns("branch-1");
        // Note there shouldn't be any more inputs required at all

        var inputs = new NewStackCommandInputs("Stack1", "branch-1", "new-branch");

        // Act
        var response = await handler.Handle(inputs);

        // Assert
        response.Should().BeEquivalentTo(new NewStackCommandResponse("Stack1", "branch-1", BranchAction.Create, "new-branch"));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["new-branch"])
        });

        inputProvider.ReceivedCalls().Should().BeEmpty();
    }
}
