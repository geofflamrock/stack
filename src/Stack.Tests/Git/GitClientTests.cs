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

        var filePath = Path.Join(repo.LocalDirectoryPath, Some.Name());

        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);

        gitClient.ChangeBranch(branch1);
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(filePath);
        repo.Commit();

        gitClient.ChangeBranch(branch2);
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(filePath);
        repo.Commit();

        // Act
        gitClient.ChangeBranch(branch1);
        var merge = () => gitClient.MergeFromLocalSourceBranch(branch2);

        // Assert
        merge.Should().Throw<MergeConflictException>();
    }
}