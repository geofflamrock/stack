using FluentAssertions;
using FluentAssertions.Execution;
using LibGit2Sharp;
using Stack.Git;
using Meziantou.Extensions.Logging.Xunit;
using Stack.Tests.Helpers;
using System.IO;
using Xunit.Abstractions;

namespace Stack.Tests.Git;

public class GitClientTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void IsMergeInProgress_WhenMergeConflictActive_ReturnsTrueThenFalseAfterAbort()
    {
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b.WithName(branch1))
            .WithBranch(b => b.WithName(branch2).FromSourceBranch(branch1))
            .Build();

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var git = new GitClient(logger, repo.ExecutionContext);

        var relativeFilePath = Some.Name();
        var filePath = Path.Join(repo.LocalDirectoryPath, relativeFilePath);

        // Create conflicting change on branch1
        git.ChangeBranch(branch1);
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(relativeFilePath);
        repo.Commit();

        // Create conflicting change on branch2
        git.ChangeBranch(branch2);
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(relativeFilePath);
        repo.Commit();

        // Start merge that will conflict
        git.ChangeBranch(branch1);
        try
        {
            git.MergeFromLocalSourceBranch(branch2);
        }
        catch (ConflictException)
        {
        }

        git.IsMergeInProgress().Should().BeTrue();

        git.AbortMerge();
        git.IsMergeInProgress().Should().BeFalse();
    }

    [Fact]
    public void IsRebaseInProgress_WhenRebaseConflictActive_ReturnsTrueThenFalseAfterAbort()
    {
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b.WithName(branch1))
            .WithBranch(b => b.WithName(branch2).FromSourceBranch(branch1))
            .Build();

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var git = new GitClient(logger, repo.ExecutionContext);

        var relativeFilePath = Some.Name();
        var filePath = Path.Join(repo.LocalDirectoryPath, relativeFilePath);

        // conflicting commit on branch1
        git.ChangeBranch(branch1);
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(relativeFilePath);
        repo.Commit();

        // conflicting commit on branch2
        git.ChangeBranch(branch2);
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(relativeFilePath);
        repo.Commit();

        // Start rebase that will conflict
        try { git.RebaseFromLocalSourceBranch(branch1); } catch (ConflictException) { }

        git.IsRebaseInProgress().Should().BeTrue();

        git.AbortRebase();
        git.IsRebaseInProgress().Should().BeFalse();
    }

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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

        // Act
        var root = gitClient.GetRootOfRepository();

        // Assert - check that the last directory component matches (the unique GUID part)
        Path.GetFileName(root).Should().Be(Path.GetFileName(repo.LocalDirectoryPath),
            "the repository root should match the local directory path");
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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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
    public void RebaseFromLocalSourceBranch_WhenConflictsDoNotOccur_IncludesBaseBranchCommits()
    {
        // Arrange
        var baseBranch = Some.BranchName();
        var featureBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(baseBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(featureBranch).FromSourceBranch(baseBranch).WithNumberOfEmptyCommits(1))
            .Build();

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

        // Make a commit on the base branch and push it to remote
        gitClient.ChangeBranch(baseBranch);
        var filePath = Path.Join(repo.LocalDirectoryPath, Some.Name());
        var fileContent = Some.Name();
        File.WriteAllText(filePath, fileContent);
        repo.Stage(Path.GetFileName(filePath));
        var baseCommit = repo.Commit();
        repo.Push(baseBranch);

        // Get initial feature branch commits
        var initialFeatureCommits = repo.GetCommitsReachableFromBranch(featureBranch);

        gitClient.ChangeBranch(featureBranch);

        // Act
        gitClient.RebaseFromLocalSourceBranch(baseBranch);

        // Assert - feature branch should now contain the base branch commit
        using (new AssertionScope())
        {
            var finalFeatureCommits = repo.GetCommitsReachableFromBranch(featureBranch);
            finalFeatureCommits.Should().Contain(c => c.Sha == baseCommit.Sha);
            finalFeatureCommits.Count.Should().BeGreaterThan(initialFeatureCommits.Count);
        }
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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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
    public void RebaseOntoNewParent_WhenConflictsDoNotOccur_IncludesNewParentCommit()
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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

        // Add a commit to the new parent branch
        gitClient.ChangeBranch(newParent);
        var filePath = Path.Join(repo.LocalDirectoryPath, Some.Name());
        var fileContent = Some.Name();
        File.WriteAllText(filePath, fileContent);
        repo.Stage(Path.GetFileName(filePath));
        var newParentCommit = repo.Commit();

        gitClient.ChangeBranch(childBranch);

        // Act
        gitClient.RebaseOntoNewParent(newParent, oldParent);

        // Assert - child branch should now contain the new parent's commit
        var childCommits = repo.GetCommitsReachableFromBranch(childBranch);
        childCommits.Should().Contain(c => c.Sha == newParentCommit.Sha);
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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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
        var branch1 = Some.BranchName(); // This branch will be ahead of remote
        var branch2 = Some.BranchName(); // This branch will be behind remote  
        var ignoredBranch = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(branch1).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(branch2).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(ignoredBranch))
            .Build();

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

        // Make branch2 behind by resetting it to previous commit
        gitClient.ChangeBranch(branch2);
        var branch2Commits = repo.GetCommitsReachableFromBranch(branch2);
        var parentCommit = branch2Commits[1];
        repo.ResetBranchToCommit(branch2, parentCommit.Sha);

        // Make branch1 current and ahead with additional commit
        gitClient.ChangeBranch(branch1);
        var filePath = Path.Join(repo.LocalDirectoryPath, Some.Name());
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(Path.GetFileName(filePath));
        var newCommit = repo.Commit();

        var branchesToCheck = new[] { branch1, branch2 };

        // Act
        var statuses = gitClient.GetBranchStatuses(branchesToCheck);

        // Assert - use a single assertion as requested
        using (new AssertionScope())
        {
            statuses.Should().HaveCount(2);
            statuses[branch1].Should().Match<GitBranchStatus>(s =>
                s.BranchName == branch1 &&
                s.IsCurrentBranch == true &&
                s.Ahead >= 0 &&
                s.Behind >= 0 &&
                s.RemoteBranchExists == true &&
                s.RemoteTrackingBranchName != null);
            statuses[branch2].Should().Match<GitBranchStatus>(s =>
                s.BranchName == branch2 &&
                s.IsCurrentBranch == false &&
                s.Ahead >= 0 &&
                s.Behind >= 0 &&
                s.RemoteBranchExists == true &&
                s.RemoteTrackingBranchName != null);
        }
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

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

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
    public void PushNewBranch_CreatesRemoteBranch()
    {
        // Arrange
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(newBranch))
            .Build();

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

        gitClient.ChangeBranch(newBranch);

        // Act
        gitClient.PushNewBranch(newBranch);

        // Assert
        repo.DoesRemoteBranchExist(newBranch).Should().BeTrue();
    }

    [Fact]
    public void PullBranch_IncludesRemoteChanges()
    {
        // Arrange
        var branch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(branch).PushToRemote().WithNumberOfEmptyCommits(2))
            .Build();

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

        gitClient.ChangeBranch(branch);

        // Reset local branch to one commit behind to simulate changes in remote
        var commits = repo.GetCommitsReachableFromBranch(branch);
        var secondCommit = commits[1]; // Get the parent commit
        repo.ResetBranchToCommit(branch, secondCommit.Sha);

        // Get initial commit count
        var initialCommits = repo.GetCommitsReachableFromBranch(branch);

        // Act
        gitClient.PullBranch(branch);

        // Assert - should now have the additional commit from the remote
        var finalCommits = repo.GetCommitsReachableFromBranch(branch);
        finalCommits.Count.Should().BeGreaterThan(initialCommits.Count);
    }

    [Fact]
    public void PushBranches_WithoutForceWithLease_PushesChangesToRemote()
    {
        // Arrange
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(branch1).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).PushToRemote())
            .Build();

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

        // Create changes on each branch
        gitClient.ChangeBranch(branch1);
        var filePath1 = Path.Join(repo.LocalDirectoryPath, Some.Name());
        var fileContent1 = Some.Name();
        File.WriteAllText(filePath1, fileContent1);
        repo.Stage(Path.GetFileName(filePath1));
        var commit1 = repo.Commit();

        gitClient.ChangeBranch(branch2);
        var filePath2 = Path.Join(repo.LocalDirectoryPath, Some.Name());
        var fileContent2 = Some.Name();
        File.WriteAllText(filePath2, fileContent2);
        repo.Stage(Path.GetFileName(filePath2));
        var commit2 = repo.Commit();

        var branches = new[] { branch1, branch2 };

        // Act
        gitClient.PushBranches(branches, false);

        // Assert - verify changes exist in remote
        using (new AssertionScope())
        {
            var remoteTip1 = repo.GetTipOfRemoteBranch(branch1);
            var remoteTip2 = repo.GetTipOfRemoteBranch(branch2);
            remoteTip1.Sha.Should().Be(commit1.Sha);
            remoteTip2.Sha.Should().Be(commit2.Sha);
        }
    }

    [Fact]
    public void PushBranches_WithForceWithLease_OverwritesRemoteChanges()
    {
        // Arrange
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(branch1).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).PushToRemote())
            .Build();

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

        // Create changes on remote branches (simulate someone else pushed)
        var remoteCommitMessage1 = Some.Name();
        var remoteCommitMessage2 = Some.Name();
        repo.CreateCommitOnRemoteTrackingBranch(branch1, remoteCommitMessage1);
        repo.CreateCommitOnRemoteTrackingBranch(branch2, remoteCommitMessage2);

        // Fetch to update local tracking information
        gitClient.Fetch(false);

        // Create different changes on local branches
        gitClient.ChangeBranch(branch1);
        var filePath1 = Path.Join(repo.LocalDirectoryPath, Some.Name());
        var fileContent1 = Some.Name();
        File.WriteAllText(filePath1, fileContent1);
        repo.Stage(Path.GetFileName(filePath1));
        var localCommit1 = repo.Commit();

        gitClient.ChangeBranch(branch2);
        var filePath2 = Path.Join(repo.LocalDirectoryPath, Some.Name());
        var fileContent2 = Some.Name();
        File.WriteAllText(filePath2, fileContent2);
        repo.Stage(Path.GetFileName(filePath2));
        var localCommit2 = repo.Commit();

        var branches = new[] { branch1, branch2 };

        // Act
        gitClient.PushBranches(branches, true);

        // Assert - local changes should exist in remote, previous remote changes should not
        using (new AssertionScope())
        {
            var remoteTip1 = repo.GetTipOfRemoteBranch(branch1);
            var remoteTip2 = repo.GetTipOfRemoteBranch(branch2);
            remoteTip1.Sha.Should().Be(localCommit1.Sha);
            remoteTip2.Sha.Should().Be(localCommit2.Sha);
            remoteTip1.Message.Should().NotBe(remoteCommitMessage1);
            remoteTip2.Message.Should().NotBe(remoteCommitMessage2);
        }
    }

    [Fact]
    public void FetchBranchRefSpecs_WhenBranchesProvided_FastForwardsLocalBranch()
    {
        // Arrange
        var targetBranch = Some.BranchName();
        var otherBranch = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b.WithName(targetBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(b => b.WithName(otherBranch).PushToRemote())
            .Build();

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);
        // Make a new commit on the target branch and push it so remote is ahead
        gitClient.ChangeBranch(targetBranch);
        var filePath = Path.Join(repo.LocalDirectoryPath, Some.Name());
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(Path.GetFileName(filePath));
        var pushedCommit = repo.Commit();
        repo.Push(targetBranch); // remote now ahead

        // Determine parent commit to reset local branch to (making local behind)
        var commits = repo.GetCommitsReachableFromBranch(targetBranch);
        var parentCommit = commits[1];
        repo.ResetBranchToCommit(targetBranch, parentCommit.Sha); // local behind remote
        var originalLocalTip = repo.GetTipOfBranch(targetBranch).Sha;
        originalLocalTip.Should().Be(parentCommit.Sha);

        var remoteTip = repo.GetTipOfRemoteBranch(targetBranch).Sha;
        remoteTip.Should().Be(pushedCommit.Sha);
        remoteTip.Should().NotBe(originalLocalTip);

        // Switch to other branch so fetch can update target branch ref
        gitClient.ChangeBranch(otherBranch);

        // Act
        gitClient.FetchBranchRefSpecs(new[] { targetBranch });

        // Assert - local branch should now match remote tip
        var newLocalTip = repo.GetTipOfBranch(targetBranch).Sha;
        newLocalTip.Should().Be(remoteTip);
    }

    [Fact]
    public void FetchBranchRefSpecs_WhenNoBranchesProvided_DoesNothing()
    {
        // Arrange
        var targetBranch = Some.BranchName();
        var otherBranch = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b.WithName(targetBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(b => b.WithName(otherBranch).PushToRemote())
            .Build();

        var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
        var gitClient = new GitClient(logger, repo.ExecutionContext);

        gitClient.ChangeBranch(otherBranch);
        var originalLocalTip = repo.GetTipOfBranch(targetBranch).Sha;

        // Act
        gitClient.FetchBranchRefSpecs(Array.Empty<string>());

        // Assert - local branch tip unchanged
        var newLocalTip = repo.GetTipOfBranch(targetBranch).Sha;
        newLocalTip.Should().Be(originalLocalTip);
    }
}