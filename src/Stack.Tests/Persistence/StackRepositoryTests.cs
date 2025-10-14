using FluentAssertions;
using NSubstitute;
using Stack.Git;
using Stack.Infrastructure.Settings;
using Stack.Persistence;
using Stack.Tests.Helpers;

namespace Stack.Tests;

public class StackRepositoryTests
{
    private class MockStackDataStore(StackData stackData) : IStackDataStore
    {
        public StackData Data { get; private set; } = stackData;

        public void Save(StackData data)
        {
            Data = data;
        }

        public StackData Load()
        {
            return Data;
        }

        public string GetConfigPath()
        {
            throw new NotImplementedException();
        }
    }

    [Fact]
    public void GetStacks_FiltersStacksByRemoteUri_CaseInsensitive()
    {
        // Arrange
        var remoteUri = Some.HttpsUri().ToString();
        var stack1 = new StackDataItem("Stack1", remoteUri, "main", []);
        var stack2 = new StackDataItem("Stack2", Some.HttpsUri().ToString(), "main", []);
        var stack3 = new StackDataItem("Stack3", remoteUri.ToUpper(), "main", []);

        var dataStore = new MockStackDataStore(new StackData([stack1, stack2, stack3]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        // Act
        var repository = new StackRepository(dataStore, gitClientFactory, executionContext);
        var stacks = repository.GetStacks();

        // Assert
        stacks.Should().BeEquivalentTo([
            new Model.Stack("Stack1", "main", []),
            new Model.Stack("Stack3", "main", [])
        ]);
    }

    [Fact]
    public void GetStacks_WhenNoRemoteUri_ReturnsEmptyList()
    {
        // Arrange
        var stack1 = new StackDataItem("Stack1", Some.HttpsUri().ToString(), "main", []);

        var dataStore = new MockStackDataStore(new StackData([stack1]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns((string?)null);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        // Act
        var repository = new StackRepository(dataStore, gitClientFactory, executionContext);
        var stacks = repository.GetStacks();

        // Assert
        stacks.Should().BeEmpty();
    }

    [Fact]
    public void GetStacks_WhenNoStacksMatchRemote_ReturnsEmptyList()
    {
        // Arrange
        var remoteUri = Some.HttpsUri().ToString();
        var stack1 = new StackDataItem("Stack1", Some.HttpsUri().ToString(), "main", []);

        var dataStore = new MockStackDataStore(new StackData([stack1]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        // Act
        var repository = new StackRepository(dataStore, gitClientFactory, executionContext);
        var stacks = repository.GetStacks();

        // Assert
        stacks.Should().BeEmpty();
    }

    [Fact]
    public void AddStack_AddsStackToCollection()
    {
        // Arrange
        var remoteUri = Some.HttpsUri().ToString();
        var existingStackInStorage = new StackDataItem("Stack1", remoteUri, "main", []);

        var dataStore = new MockStackDataStore(new StackData([existingStackInStorage]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        var repository = new StackRepository(dataStore, gitClientFactory, executionContext);

        // Act
        var newStack = new Model.Stack("Stack2", "main", []);
        repository.AddStack(newStack);
        var stacks = repository.GetStacks();

        // Assert
        stacks.Should().BeEquivalentTo([
            new Model.Stack("Stack1", "main", []),
            new Model.Stack("Stack2", "main", [])
        ]);
    }

    [Fact]
    public void RemoveStack_RemovesStackFromCollection()
    {
        // Arrange
        var remoteUri = Some.HttpsUri().ToString();
        var stack1 = new StackDataItem("Stack1", remoteUri, "main", []);
        var stack2 = new StackDataItem("Stack2", remoteUri, "main", []);

        var dataStore = new MockStackDataStore(new StackData([stack1, stack2]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        var repository = new StackRepository(dataStore, gitClientFactory, executionContext);

        // Act
        repository.RemoveStack(new Model.Stack("Stack1", "main", []));
        var stacks = repository.GetStacks();

        // Assert
        stacks.Should().BeEquivalentTo([new Model.Stack("Stack2", "main", [])]);
    }

    [Fact]
    public void SaveChanges_AfterAddingStack_SavesUpdatedData()
    {
        // Arrange
        var remoteUri = Some.HttpsUri().ToString();
        var stack1 = new StackDataItem("Stack1", remoteUri, "main", []);
        var stack2 = new StackDataItem("Stack2", remoteUri, "main", []);

        var dataStore = new MockStackDataStore(new StackData([stack1]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        var repository = new StackRepository(dataStore, gitClientFactory, executionContext);

        // Act
        repository.AddStack(new Model.Stack("Stack2", "main", []));
        repository.SaveChanges();

        // Assert
        dataStore.Data.Stacks.Should().BeEquivalentTo([stack1, stack2]);
    }

    [Fact]
    public void SaveChanges_AfterRemovingStack_SavesUpdatedData()
    {
        // Arrange
        var remoteUri = Some.HttpsUri().ToString();
        var stack1 = new StackDataItem("Stack1", remoteUri, "main", []);
        var stack2 = new StackDataItem("Stack2", remoteUri, "main", []);

        var dataStore = new MockStackDataStore(new StackData([stack1, stack2]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        var repository = new StackRepository(dataStore, gitClientFactory, executionContext);

        // Act
        repository.RemoveStack(new Model.Stack("Stack1", "main", []));
        repository.SaveChanges();

        // Assert
        dataStore.Data.Stacks.Should().BeEquivalentTo([stack2]);
    }

    [Fact]
    public void AddStack_ThenRemoveStack_ResultsInOriginalState()
    {
        // Arrange
        var remoteUri = Some.HttpsUri().ToString();
        var stack1 = new StackDataItem("Stack1", remoteUri, "main", []);

        var dataStore = new MockStackDataStore(new StackData([stack1]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        var repository = new StackRepository(dataStore, gitClientFactory, executionContext);

        // Act
        var stack2Model = new Model.Stack("Stack2", "main", []);
        repository.AddStack(stack2Model);
        repository.RemoveStack(stack2Model);

        // Assert
        dataStore.Data.Stacks.Should().BeEquivalentTo([stack1]);
    }

    [Fact]
    public void WhenExecutionContextHasSpecificWorkingDirectory_UsesGitClientForThatWorkingDirectory()
    {
        // Arrange
        var workingDirectory = "/custom/path";
        var remoteUri = Some.HttpsUri().ToString();

        var dataStore = new MockStackDataStore(new StackData([]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(workingDirectory).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = workingDirectory };

        // Act
        var repository = new StackRepository(dataStore, gitClientFactory, executionContext);

        // Assert
        repository.GetStacks();
        gitClientFactory.Received(1).Create(workingDirectory);
    }
}
