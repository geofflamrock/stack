using LibGit2Sharp;
using Stack.Git;

namespace Stack.Tests.Helpers;


public class BranchBuilder
{
    string? name;
    bool pushToRemote;
    string? sourceBranch;
    int numberOfEmptyCommits;
    List<Action<CommitBuilder>> commitBuilders = [];

    public BranchBuilder WithName(string name)
    {
        this.name = name;
        return this;
    }

    public BranchBuilder FromSourceBranch(string sourceBranch)
    {
        this.sourceBranch = sourceBranch;
        return this;
    }

    public BranchBuilder PushToRemote()
    {
        this.pushToRemote = true;
        return this;
    }

    public BranchBuilder WithNumberOfEmptyCommits(int numberOfCommits)
    {
        this.numberOfEmptyCommits = numberOfCommits;
        return this;
    }

    public BranchBuilder WithCommit(Action<CommitBuilder> commitBuilder)
    {
        commitBuilders.Add(commitBuilder);
        return this;
    }

    public void Build(Repository repository, string defaultBranchName)
    {
        var branchName = name ?? Some.BranchName();
        var defaultBranch = repository.Branches[defaultBranchName];
        var target = sourceBranch is not null ? repository.Branches[sourceBranch].Tip : defaultBranch.Tip;
        var branch = repository.CreateBranch(branchName, target);

        for (var i = 0; i < numberOfEmptyCommits; i++)
        {
            CreateEmptyCommit(repository, branch!, $"Empty commit {i + 1}");
        }

        foreach (var commitBuilder in commitBuilders)
        {
            var builder = new CommitBuilder();
            builder = builder.OnBranch(r => branch!.CanonicalName);
            commitBuilder(builder);
            builder.Build(repository);
        }

        if (pushToRemote)
        {
            repository.Branches.Update(branch,
                b => b.Remote = repository.Network.Remotes["origin"].Name,
                b => b.UpstreamBranch = branch!.CanonicalName);
            repository.Network.Push(branch);
        }
    }

    private static LibGit2Sharp.Commit CreateEmptyCommit(Repository repository, Branch branch, string message)
    {
        repository.Refs.UpdateTarget("HEAD", branch.CanonicalName);
        var signature = new Signature(Some.Name(), Some.Name(), DateTimeOffset.Now);

        return repository.Commit(message, signature, signature, new CommitOptions() { AllowEmptyCommit = true });
    }
}

public class CommitBuilder
{
    Func<Repository, string>? getBranchName;
    string? message;
    string? authorName;
    string? authorEmail;
    string? committerName;
    string? committerEmail;
    bool allowEmptyCommit;
    bool pushToRemote;
    List<(string Path, string Contents)> changes = [];

    public CommitBuilder OnBranch(string branch)
    {
        getBranchName = (_) => branch;
        return this;
    }

    public CommitBuilder OnBranch(Func<Repository, string> getBranchName)
    {
        this.getBranchName = getBranchName;
        return this;
    }

    public CommitBuilder WithChanges(string path, string contents)
    {
        changes.Add((path, contents));
        return this;
    }

    public CommitBuilder WithMessage(string message)
    {
        this.message = message;
        return this;
    }

    public CommitBuilder WithAuthor(string name, string email)
    {
        authorName = name;
        authorEmail = email;
        return this;
    }

    public CommitBuilder WithCommitter(string name, string email)
    {
        committerName = name;
        committerEmail = email;
        return this;
    }

    public CommitBuilder AllowEmptyCommit()
    {
        allowEmptyCommit = true;
        return this;
    }

    public CommitBuilder PushToRemote()
    {
        pushToRemote = true;
        return this;
    }

    public void Build(Repository repository)
    {
        Branch? branch = null;

        if (getBranchName is not null)
        {
            var branchName = getBranchName(repository);
            branch = repository.Branches[branchName];
        }

        if (branch is not null)
        {
            repository.Refs.UpdateTarget("HEAD", branch.CanonicalName);
        }

        foreach (var (path, contents) in changes)
        {
            var fullPath = Path.Combine(repository.Info.WorkingDirectory, path);
            var directory = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directory!);
            File.WriteAllText(fullPath, contents);
            LibGit2Sharp.Commands.Stage(repository, path);
        }

        var signature = new Signature(authorName ?? Some.Name(), authorEmail ?? Some.Name(), DateTimeOffset.Now);
        var committer = new Signature(committerName ?? Some.Name(), committerEmail ?? Some.Name(), DateTimeOffset.Now);

        repository.Commit(message ?? Some.Name(), signature, committer, new CommitOptions() { AllowEmptyCommit = allowEmptyCommit });

        if (branch is not null && pushToRemote)
        {
            repository.Network.Push(branch);
        }
    }
}

public class TestGitRepositoryBuilder
{
    List<Action<BranchBuilder>> branchBuilders = [];
    List<Action<CommitBuilder>> commitBuilders = [];
    Dictionary<string, string> config = new();

    public TestGitRepositoryBuilder WithBranch(string branch)
    {
        branchBuilders.Add(b => b.WithName(branch));
        return this;
    }

    public TestGitRepositoryBuilder WithBranch(string branch, bool pushToRemote)
    {
        branchBuilders.Add(b =>
        {
            b.WithName(branch);

            if (pushToRemote)
            {
                b.PushToRemote();
            }
        });
        return this;
    }

    public TestGitRepositoryBuilder WithBranch(Action<BranchBuilder> branchBuilder)
    {
        branchBuilders.Add(branchBuilder);
        return this;
    }

    public TestGitRepositoryBuilder WithNumberOfEmptyCommits(string branchName, int number, bool pushToRemote)
    {
        for (var i = 0; i < number; i++)
        {
            commitBuilders.Add(b =>
            {
                b.OnBranch(branchName).WithMessage($"Empty commit {i + 1}").AllowEmptyCommit();

                if (pushToRemote)
                {
                    b.PushToRemote();
                }
            });
        }
        return this;
    }

    public TestGitRepositoryBuilder WithNumberOfEmptyCommits(Action<CommitBuilder> commitBuilder, int number)
    {
        for (var i = 0; i < number; i++)
        {
            commitBuilders.Add(b =>
            {
                commitBuilder(b);
                b.AllowEmptyCommit();
            });
        }
        return this;
    }

    public TestGitRepositoryBuilder WithNumberOfEmptyCommitsOnRemoteTrackingBranchOf(string branch, int number, Action<CommitBuilder> commitBuilder)
    {
        for (var i = 0; i < number; i++)
        {
            commitBuilders.Add(b =>
            {
                commitBuilder(b);
                b.OnBranch(r => r.Branches[branch].TrackedBranch.CanonicalName);
                b.AllowEmptyCommit();
            });
        }
        return this;
    }

    public TestGitRepositoryBuilder WithConfig(string key, string value)
    {
        config[key] = value;
        return this;
    }

    public TestGitRepository Build()
    {
        var remote = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"), ".git");
        var remoteDirectory = new TemporaryDirectory(remote);
        var localDirectory = TemporaryDirectory.Create();

        var remoteRepo = new Repository(Repository.Init(remoteDirectory.DirectoryPath, true));
        var localRepo = new Repository(Repository.Clone(remote, localDirectory.DirectoryPath));

        // Ensure that we can commit to this repository when tests run
        // in a context where the user's name and email are not set.
        localRepo.Config.Add("user.name", Some.Name());
        localRepo.Config.Add("user.email", Some.Email());

        var defaultBranchName = Some.BranchName();

        localRepo.Refs.UpdateTarget("HEAD", "refs/heads/" + defaultBranchName);

        CreateInitialCommit(localRepo);

        foreach (var branchBuilder in branchBuilders)
        {
            var builder = new BranchBuilder();
            branchBuilder(builder);
            builder.Build(localRepo, defaultBranchName);
        }

        foreach (var commitBuilder in commitBuilders)
        {
            var builder = new CommitBuilder();
            commitBuilder(builder);
            builder.Build(localRepo);
        }

        foreach (var (key, value) in config)
        {
            localRepo.Config.Add(key, value);
        }

        return new TestGitRepository(localDirectory, remoteDirectory, localRepo);
    }

    private static LibGit2Sharp.Commit CreateInitialCommit(Repository repository)
    {
        var message = $"Initial commit";
        var signature = new Signature(Some.Name(), Some.Name(), DateTimeOffset.Now);

        return repository.Commit(message, signature, signature);
    }
}

public class TestGitRepository(TemporaryDirectory LocalDirectory, TemporaryDirectory RemoteDirectory, Repository LocalRepository) : IDisposable
{
    public string RemoteUri => RemoteDirectory.DirectoryPath;
    public string LocalDirectoryPath => LocalDirectory.DirectoryPath;
    public GitClientSettings GitClientSettings => new GitClientSettings(true, LocalDirectory.DirectoryPath);

    public LibGit2Sharp.Commit GetTipOfBranch(string branchName)
    {
        return LocalRepository.Branches[branchName].Tip;
    }

    public List<LibGit2Sharp.Commit> GetCommitsReachableFromBranch(string branchName)
    {
        return [.. LocalRepository.Branches[branchName].Commits];
    }

    public LibGit2Sharp.Commit GetTipOfRemoteBranch(string branchName)
    {
        var branch = LocalRepository.Branches[branchName];
        var remoteBranchName = branch.TrackedBranch.CanonicalName;
        return LocalRepository.Branches[remoteBranchName].Tip;
    }

    public List<LibGit2Sharp.Commit> GetCommitsReachableFromRemoteBranch(string branchName)
    {
        var branch = LocalRepository.Branches[branchName];
        var remoteBranchName = branch.TrackedBranch.CanonicalName;
        return [.. LocalRepository.Branches[remoteBranchName].Commits];
    }

    public List<LibGit2Sharp.Branch> GetBranches()
    {
        return [.. LocalRepository.Branches];
    }

    public LibGit2Sharp.Commit Commit(string? message = null)
    {
        var signature = new Signature(Some.Name(), Some.Name(), DateTimeOffset.Now);
        return LocalRepository.Commit(message ?? Some.Name(), signature, signature);
    }

    public void Stage(string path)
    {
        LibGit2Sharp.Commands.Stage(LocalRepository, path);
    }

    public void RebaseCommits(string branchName, string sourceBranchName)
    {
        var branch = LocalRepository.Branches[branchName];
        var sourceBranch = LocalRepository.Branches[sourceBranchName];
        var remoteBranchName = branch.TrackedBranch.CanonicalName;
        LocalRepository.Rebase.Start(branch, LocalRepository.Branches[remoteBranchName], sourceBranch, new Identity(Some.Name(), Some.Email()), new RebaseOptions());
    }

    public (int Ahead, int Behind) GetAheadBehind(string branchName)
    {
        var branch = LocalRepository.Branches[branchName];
        var remoteBranchName = branch.TrackedBranch.CanonicalName;
        var historyDivergence = LocalRepository.ObjectDatabase.CalculateHistoryDivergence(branch.Tip, LocalRepository.Branches[remoteBranchName].Tip);
        return (historyDivergence.AheadBy ?? 0, historyDivergence.BehindBy ?? 0);
    }

    public void DeleteRemoteTrackingBranch(string branchName)
    {
        var branch = LocalRepository.Branches[branchName];
        var remoteBranchName = branch.TrackedBranch.CanonicalName;
        LocalRepository.Branches.Remove(remoteBranchName);
    }

    public void DeleteLocalBranch(string branchName)
    {
        LocalRepository.Branches.Remove(branchName);
    }

    public void Push(string branchName)
    {
        LocalRepository.Network.Push(LocalRepository.Branches[branchName]);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        LocalRepository.Dispose();
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
