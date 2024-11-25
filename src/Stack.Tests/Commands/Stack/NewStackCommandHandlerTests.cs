using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
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
        var inputProvider = Substitute.For<IInputProvider>();
        var handler = new NewStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(["branch-1", "branch-2"]);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns("branch-1");
        inputProvider.Confirm(Questions.ConfirmAddOrCreateBranch).Returns(true);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.Create);
        inputProvider.Text(Questions.BranchName).Returns("new-branch");
        inputProvider.Confirm(Questions.ConfirmSwitchToBranch).Returns(true);

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
        var inputProvider = Substitute.For<IInputProvider>();
        var handler = new NewStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(["branch-1", "branch-2"]);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns("branch-1");
        inputProvider.Confirm(Questions.ConfirmAddOrCreateBranch).Returns(true);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.Add);
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns("branch-2");
        inputProvider.Confirm(Questions.ConfirmSwitchToBranch).Returns(true);

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
        var inputProvider = Substitute.For<IInputProvider>();
        var handler = new NewStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(["branch-1", "branch-2"]);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));


        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns("branch-1");
        inputProvider.Confirm(Questions.ConfirmAddOrCreateBranch).Returns(false);

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
        var inputProvider = Substitute.For<IInputProvider>();
        var handler = new NewStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(["branch-1", "branch-2"]);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));


        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns("branch-1");
        inputProvider.Confirm(Questions.ConfirmAddOrCreateBranch).Returns(true);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.Create);
        inputProvider.Text(Questions.BranchName).Returns("new-branch-1");
        inputProvider.Confirm(Questions.ConfirmSwitchToBranch).Returns(false);

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
        var inputProvider = Substitute.For<IInputProvider>();
        var handler = new NewStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(["branch-1", "branch-2"]);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns("branch-1");
        inputProvider.Confirm(Questions.ConfirmAddOrCreateBranch).Returns(true);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.Add);
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns("branch-2");
        inputProvider.Confirm(Questions.ConfirmSwitchToBranch).Returns(false);

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
        var inputProvider = Substitute.For<IInputProvider>();
        var handler = new NewStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(["branch-1", "branch-2"]);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns("branch-1");
        inputProvider.Confirm(Questions.ConfirmAddOrCreateBranch).Returns(false);

        var inputs = new NewStackCommandInputs("Stack1", null, null);

        // Act
        var response = await handler.Handle(inputs);

        // Assert
        response.Should().BeEquivalentTo(new NewStackCommandResponse("Stack1", "branch-1", null, null));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, "branch-1", [])
        });

        inputProvider.DidNotReceive().Text(Questions.StackName);
    }

    [Fact]
    public async Task WhenSourceBranchIsProvidedInInputs_TheProviderIsNotAskedForTheBranch_AndTheStackIsCreatedWithTheSourceBranch()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var handler = new NewStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(["branch-1", "branch-2"]);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmAddOrCreateBranch).Returns(false);

        var inputs = new NewStackCommandInputs(null, "branch-1", null);

        // Act
        var response = await handler.Handle(inputs);

        // Assert
        response.Should().BeEquivalentTo(new NewStackCommandResponse("Stack1", "branch-1", null, null));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, "branch-1", [])
        });

        inputProvider.DidNotReceive().Select(Questions.SelectBranch, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenBranchNameIsProvidedInInputs_TheProviderIsNotAskedForTheBranchName_AndTheStackIsCreatedWithTheBranchName()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var handler = new NewStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(["branch-1", "branch-2"]);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns("branch-1");
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

        inputProvider.Received().Text(Questions.StackName);
        inputProvider.Received().Select(Questions.SelectSourceBranch, Arg.Any<string[]>());
        inputProvider.ClearReceivedCalls();
        inputProvider.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task WhenAllInputsAreProvided_TheProviderIsNotAskedForAnything_AndTheStackIsCreatedCorrectly()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var handler = new NewStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(["branch-1", "branch-2"]);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

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
