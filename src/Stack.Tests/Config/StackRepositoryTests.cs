using FluentAssertions;
using FluentAssertions.Execution;
using NSubstitute;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure.Settings;
using Stack.Tests.Helpers;
using Xunit;
using StackModel = Stack.Config.Stack;

// Deliberately using Stack.Tests namespace to avoid naming conflict 
// with Stack.Config.Stack class
namespace Stack.Tests;

public class StackRepositoryTests
{
    [Fact]
    public void GetStacks_FiltersStacksByRemoteUri_CaseInsensitive()
    {
        // Arrange
        var remoteUri = "https://github.com/user/repo.git";
        var stack1 = new Config.Stack("Stack1", remoteUri, "main", []);
        var stack2 = new Config.Stack("Stack2", "https://github.com/other/repo.git", "main", []);
        var stack3 = new Config.Stack("Stack3", remoteUri.ToUpper(), "main", []);

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
        using (new AssertionScope())
        {
            stacks.Should().HaveCount(2);
            stacks.Should().Contain(stack1);
            stacks.Should().Contain(stack3);
            stacks.Should().NotContain(stack2);
        }
    }

    [Fact]
    public void GetStacks_WhenNoRemoteUri_ReturnsEmptyList()
    {
        // Arrange
        var stack1 = new StackModel("Stack1", "https://github.com/user/repo.git", "main", []);

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
        var remoteUri = "https://github.com/user/repo.git";
        var stack1 = new StackModel("Stack1", "https://github.com/other/repo.git", "main", []);

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
    public void GetStack_ReturnsStackByName_CaseInsensitive()
    {
        // Arrange
        var remoteUri = "https://github.com/user/repo.git";
        var stack1 = new StackModel("MyStack", remoteUri, "main", []);
        var stack2 = new StackModel("OtherStack", remoteUri, "main", []);

        var stackConfig = Substitute.For<IStackConfig>();
        stackConfig.Load().Returns(new StackData([stack1, stack2]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        var repository = new StackRepository(stackConfig, gitClientFactory, executionContext);

        // Act
        var stack = repository.GetStack("MYSTACK"); // Different case

        // Assert
        stack.Should().NotBeNull();
        stack!.Name.Should().Be("MyStack");
    }

    [Fact]
    public void GetStack_WhenStackNotFound_ReturnsNull()
    {
        // Arrange
        var remoteUri = "https://github.com/user/repo.git";
        var stack1 = new StackModel("MyStack", remoteUri, "main", []);

        var stackConfig = Substitute.For<IStackConfig>();
        stackConfig.Load().Returns(new StackData([stack1]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        var repository = new StackRepository(stackConfig, gitClientFactory, executionContext);

        // Act
        var stack = repository.GetStack("NonExistent");

        // Assert
        stack.Should().BeNull();
    }

    [Fact]
    public void GetStack_WhenStackExistsButForDifferentRemote_ReturnsNull()
    {
        // Arrange
        var remoteUri = "https://github.com/user/repo.git";
        var stack1 = new StackModel("MyStack", "https://github.com/other/repo.git", "main", []);

        var stackConfig = Substitute.For<IStackConfig>();
        stackConfig.Load().Returns(new StackData([stack1]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        var repository = new StackRepository(stackConfig, gitClientFactory, executionContext);

        // Act
        var stack = repository.GetStack("MyStack");

        // Assert
        stack.Should().BeNull();
    }

    [Fact]
    public void AddStack_AddsStackToCollection()
    {
        // Arrange
        var remoteUri = "https://github.com/user/repo.git";
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
        using (new AssertionScope())
        {
            stacks.Should().HaveCount(2);
            stacks.Should().Contain(existingStack);
            stacks.Should().Contain(newStack);
        }
    }

    [Fact]
    public void RemoveStack_RemovesStackFromCollection()
    {
        // Arrange
        var remoteUri = "https://github.com/user/repo.git";
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
        using (new AssertionScope())
        {
            stacks.Should().HaveCount(1);
            stacks.Should().Contain(stack2);
            stacks.Should().NotContain(stack1);
        }
    }

    [Fact]
    public void SaveChanges_CallsStackConfigSave()
    {
        // Arrange
        var remoteUri = "https://github.com/user/repo.git";
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
        var remoteUri = "https://github.com/user/repo.git";
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
        var remoteUri = "https://github.com/user/repo.git";
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
    public void RemoteUri_ReturnsRemoteUriFromGitClient()
    {
        // Arrange
        var remoteUri = "https://github.com/user/repo.git";

        var stackConfig = Substitute.For<IStackConfig>();
        stackConfig.Load().Returns(new StackData([]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        // Act
        var repository = new StackRepository(stackConfig, gitClientFactory, executionContext);

        // Assert
        repository.RemoteUri.Should().Be(remoteUri);
    }

    [Fact]
    public void RemoteUri_WhenNull_ReturnsEmptyString()
    {
        // Arrange
        var stackConfig = Substitute.For<IStackConfig>();
        stackConfig.Load().Returns(new StackData([]));

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns((string?)null);

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };

        // Act
        var repository = new StackRepository(stackConfig, gitClientFactory, executionContext);

        // Assert
        repository.RemoteUri.Should().Be(string.Empty);
    }

    [Fact]
    public void AddStack_ThenRemoveStack_ResultsInOriginalState()
    {
        // Arrange
        var remoteUri = "https://github.com/user/repo.git";
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
        using (new AssertionScope())
        {
            stacks.Should().HaveCount(1);
            stacks.Should().Contain(stack1);
            stacks.Should().NotContain(stack2);
        }
    }

    [Fact]
    public void GetStacks_ReturnsNewListEachTime()
    {
        // Arrange
        var remoteUri = "https://github.com/user/repo.git";
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
    public void Constructor_UsesWorkingDirectoryFromExecutionContext()
    {
        // Arrange
        var workingDirectory = "/custom/path";
        var remoteUri = "https://github.com/user/repo.git";

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
        gitClientFactory.Received(1).Create(workingDirectory);
    }
}
