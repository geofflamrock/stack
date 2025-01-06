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

        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);

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
        merge.Should().Throw<MergeConflictException>();
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

        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);

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
}