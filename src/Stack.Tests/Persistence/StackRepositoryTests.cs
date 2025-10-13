using FluentAssertions;
using NSubstitute;
using Stack.Git;
using Stack.Infrastructure.Settings;
using Stack.Persistence;
using Stack.Tests.Helpers;
using StackModel = Stack.Model.Stack;

namespace Stack.Tests;

public class StackRepositoryTests
{
    [Fact]
    public void GetStacks_FiltersStacksByRemoteUri_CaseInsensitive()
    {
        // Arrange
        var remoteUri = Some.HttpsUri().ToString();
        var stack1 = new StackModel("Stack1", remoteUri, "main", []);
        var stack2 = new StackModel("Stack2", Some.HttpsUri().ToString(), "main", []);
        var stack3 = new StackModel("Stack3", remoteUri.ToUpper(), "main", []);

        var stackConfig = Substitute.For<IStackConfig>();
        stackConfig.Load().Returns(new StackData([stack1, stack2, stack3]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        // Act
        var repository = new StackRepository(stackConfig, gitClientFactory, executionContext);
        var stacks = repository.GetStacks();

        // Assert
        stacks.Should().BeEquivalentTo([stack1, stack3]);
    }

    [Fact]
    public void GetStacks_WhenNoRemoteUri_ReturnsEmptyList()
    {
        // Arrange
        var stack1 = new StackModel("Stack1", Some.HttpsUri().ToString(), "main", []);

        var stackConfig = Substitute.For<IStackConfig>();
        stackConfig.Load().Returns(new StackData([stack1]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns((string?)null);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        // Act
        var repository = new StackRepository(stackConfig, gitClientFactory, executionContext);
        var stacks = repository.GetStacks();

        // Assert
        stacks.Should().BeEmpty();
    }

    [Fact]
    public void GetStacks_WhenNoStacksMatchRemote_ReturnsEmptyList()
    {
        // Arrange
        var remoteUri = Some.HttpsUri().ToString();
        var stack1 = new StackModel("Stack1", Some.HttpsUri().ToString(), "main", []);

        var stackConfig = Substitute.For<IStackConfig>();
        stackConfig.Load().Returns(new StackData([stack1]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        // Act
        var repository = new StackRepository(stackConfig, gitClientFactory, executionContext);
        var stacks = repository.GetStacks();

        // Assert
        stacks.Should().BeEmpty();
    }

    [Fact]
    public void AddStack_AddsStackToCollection()
    {
        // Arrange
        var remoteUri = Some.HttpsUri().ToString();
        var existingStack = new StackModel("Stack1", remoteUri, "main", []);
        var newStack = new StackModel("Stack2", remoteUri, "main", []);

        var stackConfig = Substitute.For<IStackConfig>();
        stackConfig.Load().Returns(new StackData([existingStack]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        var repository = new StackRepository(stackConfig, gitClientFactory, executionContext);

        // Act
        repository.AddStack(newStack);
        var stacks = repository.GetStacks();

        // Assert
        stacks.Should().BeEquivalentTo([existingStack, newStack]);
    }

    [Fact]
    public void RemoveStack_RemovesStackFromCollection()
    {
        // Arrange
        var remoteUri = Some.HttpsUri().ToString();
        var stack1 = new StackModel("Stack1", remoteUri, "main", []);
        var stack2 = new StackModel("Stack2", remoteUri, "main", []);

        var stackConfig = Substitute.For<IStackConfig>();
        stackConfig.Load().Returns(new StackData([stack1, stack2]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        var repository = new StackRepository(stackConfig, gitClientFactory, executionContext);

        // Act
        repository.RemoveStack(stack1);
        var stacks = repository.GetStacks();

        // Assert
        stacks.Should().BeEquivalentTo([stack2]);
    }

    [Fact]
    public void SaveChanges_CallsStackConfigSave()
    {
        // Arrange
        var remoteUri = Some.HttpsUri().ToString();
        var stack1 = new StackModel("Stack1", remoteUri, "main", []);
        var originalStackData = new StackData([stack1]);

        var stackConfig = Substitute.For<IStackConfig>();
        stackConfig.Load().Returns(originalStackData);

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        var repository = new StackRepository(stackConfig, gitClientFactory, executionContext);

        // Act
        repository.SaveChanges();

        // Assert
        stackConfig.Received(1).Save(Arg.Is<StackData>(sd => sd == originalStackData));
    }

    [Fact]
    public void SaveChanges_AfterAddingStack_SavesUpdatedData()
    {
        // Arrange
        var remoteUri = Some.HttpsUri().ToString();
        var stack1 = new StackModel("Stack1", remoteUri, "main", []);
        var stack2 = new StackModel("Stack2", remoteUri, "main", []);

        var stackConfig = Substitute.For<IStackConfig>();
        stackConfig.Load().Returns(new StackData([stack1]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        var repository = new StackRepository(stackConfig, gitClientFactory, executionContext);

        // Act
        repository.AddStack(stack2);
        repository.SaveChanges();

        // Assert
        stackConfig.Received(1).Save(Arg.Is<StackData>(sd =>
            sd.Stacks.Count == 2 &&
            sd.Stacks.Contains(stack1) &&
            sd.Stacks.Contains(stack2)));
    }

    [Fact]
    public void SaveChanges_AfterRemovingStack_SavesUpdatedData()
    {
        // Arrange
        var remoteUri = Some.HttpsUri().ToString();
        var stack1 = new StackModel("Stack1", remoteUri, "main", []);
        var stack2 = new StackModel("Stack2", remoteUri, "main", []);

        var stackConfig = Substitute.For<IStackConfig>();
        stackConfig.Load().Returns(new StackData([stack1, stack2]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        var repository = new StackRepository(stackConfig, gitClientFactory, executionContext);

        // Act
        repository.RemoveStack(stack1);
        repository.SaveChanges();

        // Assert
        stackConfig.Received(1).Save(Arg.Is<StackData>(sd =>
            sd.Stacks.Count == 1 &&
            sd.Stacks.Contains(stack2) &&
            !sd.Stacks.Contains(stack1)));
    }

    [Fact]
    public void AddStack_ThenRemoveStack_ResultsInOriginalState()
    {
        // Arrange
        var remoteUri = Some.HttpsUri().ToString();
        var stack1 = new StackModel("Stack1", remoteUri, "main", []);
        var stack2 = new StackModel("Stack2", remoteUri, "main", []);

        var stackConfig = Substitute.For<IStackConfig>();
        stackConfig.Load().Returns(new StackData([stack1]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        var repository = new StackRepository(stackConfig, gitClientFactory, executionContext);

        // Act
        repository.AddStack(stack2);
        repository.RemoveStack(stack2);
        var stacks = repository.GetStacks();

        // Assert
        stacks.Should().BeEquivalentTo([stack1]);
    }

    [Fact]
    public void GetStacks_ReturnsNewListEachTime()
    {
        // Arrange
        var remoteUri = Some.HttpsUri().ToString();
        var stack1 = new StackModel("Stack1", remoteUri, "main", []);

        var stackConfig = Substitute.For<IStackConfig>();
        stackConfig.Load().Returns(new StackData([stack1]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        var repository = new StackRepository(stackConfig, gitClientFactory, executionContext);

        // Act
        var stacks1 = repository.GetStacks();
        var stacks2 = repository.GetStacks();

        // Assert
        stacks1.Should().NotBeSameAs(stacks2, "each call should return a new list instance");
    }

    [Fact]
    public void WhenExecutionContextHasSpecificWorkingDirectory_UsesGitClientForThatWorkingDirectory()
    {
        // Arrange
        var workingDirectory = "/custom/path";
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = Substitute.For<IStackConfig>();
        stackConfig.Load().Returns(new StackData([]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(workingDirectory).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = workingDirectory };

        // Act
        var repository = new StackRepository(stackConfig, gitClientFactory, executionContext);

        // Assert
        repository.GetStacks();
        gitClientFactory.Received(1).Create(workingDirectory);
    }
}
