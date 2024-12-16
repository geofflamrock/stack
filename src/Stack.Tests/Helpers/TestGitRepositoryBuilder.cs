using System;
using LibGit2Sharp;
using Microsoft.VisualBasic;
using Stack.Git;

namespace Stack.Tests.Helpers;

public class TestGitRepositoryBuilder
{
    List<(string Name, bool PushToRemote)> branches = [];

    public TestGitRepositoryBuilder WithBranch(string branch)
    {
        branches.Add((branch, false));
        return this;
    }

    public TestGitRepositoryBuilder WithBranch(string branch, bool pushToRemote)
    {
        branches.Add((branch, pushToRemote));
        return this;
    }

    public TestGitRepository Build()
    {
        var remote = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"), ".git");
        var remoteDirectory = new TemporaryDirectory(remote);
        var localDirectory = TemporaryDirectory.Create();

        var remoteRepo = new Repository(Repository.Init(remoteDirectory.DirectoryPath, true));
        var repo = new Repository(Repository.Clone(remote, localDirectory.DirectoryPath));
        var defaultBranch = Some.BranchName();
        repo.Refs.UpdateTarget("HEAD", "refs/heads/" + defaultBranch);

        Commit(repo, ("README.md", "Hello, World!"));

        foreach (var branch in branches)
        {
            repo.CreateBranch(branch.Name);

            if (branch.PushToRemote)
            {
                repo.Branches.Update(repo.Branches[branch.Name],
                    b => b.Remote = repo.Network.Remotes["origin"].Name,
                    b => b.UpstreamBranch = $"refs/heads/{branch.Name}");
                repo.Network.Push(repo.Branches[branch.Name]);
            }
        }

        return new TestGitRepository(localDirectory, remoteDirectory, repo);
    }

    private Commit Commit(Repository repository, params (string Name, string? Content)[] files)
    {
        var message = $"Commit: {Some.Name()}";
        var signature = new Signature(Some.Name(), Some.Name(), DateTimeOffset.Now);

        return repository.Commit(message, signature, signature);
    }
}

public class TestGitRepository(TemporaryDirectory LocalDirectory, TemporaryDirectory RemoteDirectory, Repository Repository) : IDisposable
{
    public TemporaryDirectory LocalDirectory { get; } = LocalDirectory;
    public TemporaryDirectory RemoteDirectory { get; } = RemoteDirectory;
    public string RemoteUri => RemoteDirectory.DirectoryPath;
    public GitOperationSettings GitOperationSettings => new GitOperationSettings(false, false, LocalDirectory.DirectoryPath);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Repository.Dispose();
        LocalDirectory.Dispose();
        RemoteDirectory.Dispose();
    }
}

public class TemporaryDirectory(string DirectoryPath) : IDisposable
{
    public string DirectoryPath { get; } = DirectoryPath;

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try
        {
            Directory.Delete(DirectoryPath, true);
        }
        catch (Exception)
        {
            // Ignore
        }
    }

    public static TemporaryDirectory Create()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new TemporaryDirectory(path);
    }
}
