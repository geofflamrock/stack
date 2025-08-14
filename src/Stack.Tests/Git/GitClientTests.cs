using FluentAssertions;
using Stack.Git;
using Stack.Tests.Helpers;
using Xunit.Abstractions;

namespace Stack.Tests.Git;

public class GitClientTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void MergeFromLocalSourceBranch_WhenConflictsOccur_ThrowsMergeConflictException()
    {
        // Arrange
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(branch1))
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1))
            .Build();

        var relativeFilePath = Some.Name();
        var filePath = Path.Join(repo.LocalDirectoryPath, relativeFilePath);

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        gitClient.ChangeBranch(branch1);
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(relativeFilePath);
        repo.Commit();

        gitClient.ChangeBranch(branch2);
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(relativeFilePath);
        repo.Commit();

        // Act
        gitClient.ChangeBranch(branch1);
        var merge = () => gitClient.MergeFromLocalSourceBranch(branch2);

        // Assert
        merge.Should().Throw<ConflictException>();
    }

    [Fact]
    public void MergeFromLocalSourceBranch_WhenConflictsDoNotOccur_DoesNotThrow()
    {
        // Arrange
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(branch1))
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1))
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        var relativeFilePath1 = Some.Name();
        var filePath1 = Path.Join(repo.LocalDirectoryPath, relativeFilePath1);

        gitClient.ChangeBranch(branch1);
        File.WriteAllText(filePath1, Some.Name());
        repo.Stage(relativeFilePath1);
        repo.Commit();

        var relativeFilePath2 = Some.Name();
        var filePath2 = Path.Join(repo.LocalDirectoryPath, relativeFilePath2);

        gitClient.ChangeBranch(branch2);
        File.WriteAllText(filePath2, Some.Name());
        repo.Stage(relativeFilePath2);
        repo.Commit();

        // Act
        gitClient.ChangeBranch(branch1);
        var merge = () => gitClient.MergeFromLocalSourceBranch(branch2);

        // Assert
        merge.Should().NotThrow();
    }

    [Fact]
    public void GetCurrentBranch_ReturnsCurrentBranchName()
    {
        // Arrange
        var expectedBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(expectedBranch))
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        gitClient.ChangeBranch(expectedBranch);

        // Act
        var currentBranch = gitClient.GetCurrentBranch();

        // Assert
        currentBranch.Should().Be(expectedBranch);
    }

    [Fact]
    public void DoesLocalBranchExist_WhenBranchExists_ReturnsTrue()
    {
        // Arrange
        var existingBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(existingBranch))
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        // Act
        var exists = gitClient.DoesLocalBranchExist(existingBranch);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public void DoesLocalBranchExist_WhenBranchDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var nonExistentBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        // Act
        var exists = gitClient.DoesLocalBranchExist(nonExistentBranch);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public void GetBranchesThatExistLocally_ReturnsBranchesThatExist()
    {
        // Arrange
        var existingBranch1 = Some.BranchName();
        var existingBranch2 = Some.BranchName();
        var nonExistentBranch = Some.BranchName();
        
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(existingBranch1))
            .WithBranch(builder => builder.WithName(existingBranch2))
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        var branchesToCheck = new[] { existingBranch1, nonExistentBranch, existingBranch2 };

        // Act
        var existingBranches = gitClient.GetBranchesThatExistLocally(branchesToCheck);

        // Assert
        existingBranches.Should().Contain(existingBranch1);
        existingBranches.Should().Contain(existingBranch2);
        existingBranches.Should().NotContain(nonExistentBranch);
        existingBranches.Length.Should().Be(2);
    }

    [Fact]
    public void CompareBranches_ReturnsCorrectAheadBehindCounts()
    {
        // Arrange
        var baseBranch = Some.BranchName();
        var featureBranch = Some.BranchName();
        
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(baseBranch))
            .WithBranch(builder => builder.WithName(featureBranch).FromSourceBranch(baseBranch))
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        // Create a commit on the feature branch to make it 1 ahead
        gitClient.ChangeBranch(featureBranch);
        var filePath = Path.Join(repo.LocalDirectoryPath, Some.Name());
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(Path.GetFileName(filePath));
        repo.Commit();

        // Act
        var (ahead, behind) = gitClient.CompareBranches(featureBranch, baseBranch);

        // Assert
        ahead.Should().Be(1);
        behind.Should().Be(0);
    }

    [Fact]
    public void GetRemoteUri_ReturnsRemoteOriginUri()
    {
        // Arrange
        using var repo = new TestGitRepositoryBuilder()
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        // Act
        var remoteUri = gitClient.GetRemoteUri();

        // Assert
        remoteUri.Should().Be(repo.RemoteUri);
    }

    [Fact]
    public void GetRootOfRepository_ReturnsRepositoryRoot()
    {
        // Arrange
        using var repo = new TestGitRepositoryBuilder()
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        // Act
        var root = gitClient.GetRootOfRepository();

        // Assert
        root.Should().Be(repo.LocalDirectoryPath);
    }

    [Fact]
    public void GetConfigValue_WhenConfigExists_ReturnsValue()
    {
        // Arrange
        var configKey = "user.name";
        var expectedValue = Some.Name();
        
        using var repo = new TestGitRepositoryBuilder()
            .WithConfig(configKey, expectedValue)
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        // Act
        var configValue = gitClient.GetConfigValue(configKey);

        // Assert
        configValue.Should().Be(expectedValue);
    }

    [Fact]
    public void GetConfigValue_WhenConfigDoesNotExist_ReturnsNull()
    {
        // Arrange
        var nonExistentKey = "non.existent.key";
        
        using var repo = new TestGitRepositoryBuilder()
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        // Act
        var configValue = gitClient.GetConfigValue(nonExistentKey);

        // Assert
        configValue.Should().BeNull();
    }

    [Fact]
    public void IsAncestor_WhenIsNotAncestor_ReturnsFalse()
    {
        // Arrange
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(branch1))
            .WithBranch(builder => builder.WithName(branch2))
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        // Create commits on both branches independently
        gitClient.ChangeBranch(branch1);
        var filePath1 = Path.Join(repo.LocalDirectoryPath, Some.Name());
        File.WriteAllText(filePath1, Some.Name());
        repo.Stage(Path.GetFileName(filePath1));
        repo.Commit();

        gitClient.ChangeBranch(branch2);
        var filePath2 = Path.Join(repo.LocalDirectoryPath, Some.Name());
        File.WriteAllText(filePath2, Some.Name());
        repo.Stage(Path.GetFileName(filePath2));
        repo.Commit();

        // Act
        var isAncestor = gitClient.IsAncestor(branch1, branch2);

        // Assert
        isAncestor.Should().BeFalse();
    }

    [Fact]
    public void ChangeBranch_SwitchesToSpecifiedBranch()
    {
        // Arrange
        var targetBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(targetBranch))
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        // Act
        gitClient.ChangeBranch(targetBranch);

        // Assert
        gitClient.GetCurrentBranch().Should().Be(targetBranch);
    }

    [Fact]
    public void CreateNewBranch_CreatesNewBranchFromSource()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch))
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        // Act
        gitClient.CreateNewBranch(newBranch, sourceBranch);

        // Assert
        gitClient.DoesLocalBranchExist(newBranch).Should().BeTrue();
    }

    [Fact]
    public void DeleteLocalBranch_DeletesSpecifiedBranch()
    {
        // Arrange
        var branchToDelete = Some.BranchName();
        var otherBranch = Some.BranchName();
        
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(branchToDelete))
            .WithBranch(builder => builder.WithName(otherBranch))
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        // Switch to a different branch before deleting
        gitClient.ChangeBranch(otherBranch);

        // Act
        gitClient.DeleteLocalBranch(branchToDelete);

        // Assert
        gitClient.DoesLocalBranchExist(branchToDelete).Should().BeFalse();
        gitClient.DoesLocalBranchExist(otherBranch).Should().BeTrue();
    }

    [Fact]
    public void Fetch_DoesNotThrow()
    {
        // Arrange
        using var repo = new TestGitRepositoryBuilder()
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        // Act & Assert
        var fetch = () => gitClient.Fetch(false);
        fetch.Should().NotThrow();
    }

    [Fact]
    public void Fetch_WithPrune_DoesNotThrow()
    {
        // Arrange
        using var repo = new TestGitRepositoryBuilder()
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        // Act & Assert
        var fetch = () => gitClient.Fetch(true);
        fetch.Should().NotThrow();
    }

    [Fact]
    public void RebaseFromLocalSourceBranch_WhenConflictsOccur_ThrowsConflictException()
    {
        // Arrange
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(branch1))
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1))
            .Build();

        var relativeFilePath = Some.Name();
        var filePath = Path.Join(repo.LocalDirectoryPath, relativeFilePath);

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        gitClient.ChangeBranch(branch1);
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(relativeFilePath);
        repo.Commit();

        gitClient.ChangeBranch(branch2);
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(relativeFilePath);
        repo.Commit();

        // Act
        var rebase = () => gitClient.RebaseFromLocalSourceBranch(branch1);

        // Assert
        rebase.Should().Throw<ConflictException>();
    }

    [Fact]
    public void RebaseFromLocalSourceBranch_WhenConflictsDoNotOccur_DoesNotThrow()
    {
        // Arrange
        var baseBranch = Some.BranchName();
        var featureBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(baseBranch))
            .WithBranch(builder => builder.WithName(featureBranch).FromSourceBranch(baseBranch).WithNumberOfEmptyCommits(1))
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        gitClient.ChangeBranch(featureBranch);

        // Act
        var rebase = () => gitClient.RebaseFromLocalSourceBranch(baseBranch);

        // Assert
        rebase.Should().NotThrow();
    }

    [Fact]
    public void RebaseOntoNewParent_WhenConflictsOccur_ThrowsConflictException()
    {
        // Arrange
        var oldParent = Some.BranchName();
        var newParent = Some.BranchName();
        var childBranch = Some.BranchName();
        
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(oldParent))
            .WithBranch(builder => builder.WithName(newParent))
            .WithBranch(builder => builder.WithName(childBranch).FromSourceBranch(oldParent))
            .Build();

        var relativeFilePath = Some.Name();
        var filePath = Path.Join(repo.LocalDirectoryPath, relativeFilePath);

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        // Create conflicting commits on newParent and childBranch
        gitClient.ChangeBranch(newParent);
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(relativeFilePath);
        repo.Commit();

        gitClient.ChangeBranch(childBranch);
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(relativeFilePath);
        repo.Commit();

        // Act
        var rebase = () => gitClient.RebaseOntoNewParent(newParent, oldParent);

        // Assert
        rebase.Should().Throw<ConflictException>();
    }

    [Fact]
    public void RebaseOntoNewParent_WhenConflictsDoNotOccur_DoesNotThrow()
    {
        // Arrange
        var oldParent = Some.BranchName();
        var newParent = Some.BranchName();
        var childBranch = Some.BranchName();
        
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(oldParent))
            .WithBranch(builder => builder.WithName(newParent))
            .WithBranch(builder => builder.WithName(childBranch).FromSourceBranch(oldParent).WithNumberOfEmptyCommits(1))
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        gitClient.ChangeBranch(childBranch);

        // Act
        var rebase = () => gitClient.RebaseOntoNewParent(newParent, oldParent);

        // Assert
        rebase.Should().NotThrow();
    }

    [Fact]
    public void AbortMerge_DoesNotThrow()
    {
        // Arrange
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(branch1))
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1))
            .Build();

        var relativeFilePath = Some.Name();
        var filePath = Path.Join(repo.LocalDirectoryPath, relativeFilePath);

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        gitClient.ChangeBranch(branch1);
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(relativeFilePath);
        repo.Commit();

        gitClient.ChangeBranch(branch2);
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(relativeFilePath);
        repo.Commit();

        gitClient.ChangeBranch(branch1);
        try
        {
            gitClient.MergeFromLocalSourceBranch(branch2);
        }
        catch (ConflictException)
        {
            // Expected - we want to be in a merge conflict state
        }

        // Act & Assert
        var abort = () => gitClient.AbortMerge();
        abort.Should().NotThrow();
    }

    [Fact]
    public void AbortRebase_DoesNotThrow()
    {
        // Arrange
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(branch1))
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1))
            .Build();

        var relativeFilePath = Some.Name();
        var filePath = Path.Join(repo.LocalDirectoryPath, relativeFilePath);

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        gitClient.ChangeBranch(branch1);
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(relativeFilePath);
        repo.Commit();

        gitClient.ChangeBranch(branch2);
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(relativeFilePath);
        repo.Commit();

        try
        {
            gitClient.RebaseFromLocalSourceBranch(branch1);
        }
        catch (ConflictException)
        {
            // Expected - we want to be in a rebase conflict state
        }

        // Act & Assert
        var abort = () => gitClient.AbortRebase();
        abort.Should().NotThrow();
    }

    [Fact]
    public void ContinueRebase_WhenConflictsRemain_ThrowsConflictException()
    {
        // Arrange
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(branch1))
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1))
            .Build();

        var relativeFilePath = Some.Name();
        var filePath = Path.Join(repo.LocalDirectoryPath, relativeFilePath);

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        gitClient.ChangeBranch(branch1);
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(relativeFilePath);
        repo.Commit();

        gitClient.ChangeBranch(branch2);
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(relativeFilePath);
        repo.Commit();

        try
        {
            gitClient.RebaseFromLocalSourceBranch(branch1);
        }
        catch (ConflictException)
        {
            // Expected - we're now in a rebase conflict state
        }

        // Act (don't resolve conflicts, just try to continue)
        var continueRebase = () => gitClient.ContinueRebase();

        // Assert
        continueRebase.Should().Throw<ConflictException>();
    }

    [Fact]
    public void GetBranchStatuses_ReturnsCorrectStatusesForRequestedBranches()
    {
        // Arrange
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var ignoredBranch = Some.BranchName();
        
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(branch1))
            .WithBranch(builder => builder.WithName(branch2))
            .WithBranch(builder => builder.WithName(ignoredBranch))
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        var branchesToCheck = new[] { branch1, branch2 };

        // Act
        var statuses = gitClient.GetBranchStatuses(branchesToCheck);

        // Assert
        statuses.Should().ContainKey(branch1);
        statuses.Should().ContainKey(branch2);
        statuses.Should().NotContainKey(ignoredBranch);
        statuses[branch1].BranchName.Should().Be(branch1);
        statuses[branch2].BranchName.Should().Be(branch2);
    }

    [Fact]
    public void GetLocalBranchesOrderedByMostRecentCommitterDate_ReturnsOrderedBranches()
    {
        // Arrange
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(branch1))
            .WithBranch(builder => builder.WithName(branch2))
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        // Create commits on branches with some delay to ensure different commit times
        gitClient.ChangeBranch(branch1);
        var filePath1 = Path.Join(repo.LocalDirectoryPath, Some.Name());
        File.WriteAllText(filePath1, Some.Name());
        repo.Stage(Path.GetFileName(filePath1));
        repo.Commit();

        Thread.Sleep(1000); // Ensure different commit times

        gitClient.ChangeBranch(branch2);
        var filePath2 = Path.Join(repo.LocalDirectoryPath, Some.Name());
        File.WriteAllText(filePath2, Some.Name());
        repo.Stage(Path.GetFileName(filePath2));
        repo.Commit();

        // Act
        var branches = gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate();

        // Assert
        branches.Should().NotBeEmpty();
        branches.Should().Contain(branch1);
        branches.Should().Contain(branch2);
        // branch2 should appear before branch1 (more recent)
        Array.IndexOf(branches, branch2).Should().BeLessThan(Array.IndexOf(branches, branch1));
    }

    [Fact]
    public void PushNewBranch_DoesNotThrow()
    {
        // Arrange
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(newBranch))
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        gitClient.ChangeBranch(newBranch);

        // Act & Assert
        var push = () => gitClient.PushNewBranch(newBranch);
        push.Should().NotThrow();
    }

    [Fact]
    public void PullBranch_DoesNotThrow()
    {
        // Arrange
        var branch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(branch).PushToRemote())
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        gitClient.ChangeBranch(branch);

        // Act & Assert
        var pull = () => gitClient.PullBranch(branch);
        pull.Should().NotThrow();
    }

    [Fact]
    public void PushBranches_WithoutForceWithLease_DoesNotThrow()
    {
        // Arrange
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(branch1).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).PushToRemote())
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        var branches = new[] { branch1, branch2 };

        // Act & Assert
        var push = () => gitClient.PushBranches(branches, false);
        push.Should().NotThrow();
    }

    [Fact]
    public void PushBranches_WithForceWithLease_DoesNotThrow()
    {
        // Arrange
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(branch1).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).PushToRemote())
            .Build();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);

        var branches = new[] { branch1, branch2 };

        // Act & Assert
        var push = () => gitClient.PushBranches(branches, true);
        push.Should().NotThrow();
    }
}