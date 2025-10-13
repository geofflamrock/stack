using System.Text;
using LibGit2Sharp;

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
    bool pushToRemote;
    List<(string Path, string? Contents)> changes = [];

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

        var signature = new Signature(Some.Name(), Some.Name(), DateTimeOffset.Now);

        Commit(repository, branch?.Tip, branch?.CanonicalName, message ?? Some.Name(), signature, changes.ToArray());

        if (branch is not null && pushToRemote)
        {
            repository.Network.Push(branch);
        }
    }

    public static LibGit2Sharp.Commit Commit(Repository repository,
        LibGit2Sharp.Commit? parent,
        string? branchName,
        string message,
        Signature? signature,
        params (string Name, string? Content)[] files)
    {
        // Commits for uninitialised repositories will have no parent, and will need to start with an empty tree.
        var treeDefinition = parent is null ? new TreeDefinition() : TreeDefinition.From(parent.Tree);

        foreach (var file in files)
        {
            if (file.Content is null)
            {
                treeDefinition.Remove(file.Name);
            }
            else
            {
                var bytes = Encoding.UTF8.GetBytes(file.Content);
                var blobId = repository.ObjectDatabase.Write<Blob>(bytes);
                treeDefinition.Add(file.Name, blobId, Mode.NonExecutableFile);
            }
        }

        return CommitTreeDefinition(repository, parent, branchName, message, signature, treeDefinition);
    }

    static LibGit2Sharp.Commit CommitTreeDefinition(Repository repository,
        LibGit2Sharp.Commit? parent,
        string? branchName,
        string message,
        Signature? signature,
        TreeDefinition treeDefinition)
    {
        // Write the tree to the object database
        var tree = repository.ObjectDatabase.CreateTree(treeDefinition);

        // Create the commit
        var parents = parent is null ? Array.Empty<LibGit2Sharp.Commit>() : new[] { parent };
        var commit = repository.ObjectDatabase.CreateCommit(
            signature,
            signature,
            message,
            tree,
            parents,
            false);

        if (branchName is not null)
        {
            // Point the branch at the new commit if a branch name
            // has been provided
            var branch = repository.Branches[branchName];

            if (branch is null)
            {
                repository.Branches.Add(branchName, commit);
            }
            else
            {
                repository.Refs.UpdateTarget(branch.Reference, commit.Id);
            }
        }

        return commit;
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
                b.OnBranch(branchName).WithMessage($"Empty commit {i + 1}");

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
    readonly List<TemporaryDirectory> WorktreePaths = [];

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

    public LibGit2Sharp.Branch ChangeBranch(string branchName)
    {
        return LibGit2Sharp.Commands.Checkout(LocalRepository, branchName);
    }

    public LibGit2Sharp.Commit Commit(string? message = null)
    {
        var signature = new Signature(Some.Name(), Some.Name(), DateTimeOffset.Now);
        return LocalRepository.Commit(message ?? Some.Name(), signature, signature);
    }

    public LibGit2Sharp.Commit Commit(string relativePath, string contents, string? message = null)
    {
        var fullPath = Path.Combine(LocalDirectoryPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, contents);
        LibGit2Sharp.Commands.Stage(LocalRepository, relativePath);

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

    public bool DoesRemoteBranchExist(string branchName)
    {
        var branch = LocalRepository.Branches[branchName];
        return branch?.TrackedBranch != null;
    }

    public void ResetBranchToCommit(string branchName, string commitSha)
    {
        var branch = LocalRepository.Branches[branchName];
        var commit = LocalRepository.Lookup<LibGit2Sharp.Commit>(commitSha);
        LocalRepository.Reset(ResetMode.Hard, commit);
    }

    public void CreateCommitOnRemoteTrackingBranch(string branchName, string message)
    {
        var branch = LocalRepository.Branches[branchName];
        var remoteBranchName = branch.TrackedBranch.CanonicalName;
        var remoteBranch = LocalRepository.Branches[remoteBranchName];

        // Create a commit directly on the remote tracking branch
        var signature = new Signature(Some.Name(), Some.Email(), DateTimeOffset.Now);
        var tree = remoteBranch.Tip.Tree;
        var parents = new[] { remoteBranch.Tip };
        var commit = LocalRepository.ObjectDatabase.CreateCommit(signature, signature, message, tree, parents, false);

        // Update the remote tracking branch to point to the new commit
        LocalRepository.Refs.UpdateTarget(remoteBranch.Reference, commit.Id);
    }

    /// <summary>
    /// Creates a "squash merge" style commit on the target branch whose tree matches the tip of the branch being squashed.
    /// This simulates a PR being squash merged into the target branch (e.g. source) without preserving the original commits.
    /// The newly created commit is added to both the local and remote tracking refs of the target branch (if a remote exists).
    /// </summary>
    /// <param name="branchToSquashName">The feature/stack branch whose cumulative changes will be squashed.</param>
    /// <param name="targetBranchName">The branch to receive the squash commit (typically the source branch).</param>
    /// <param name="message">The commit message for the squash commit.</param>
    /// <returns>The created squash commit.</returns>
    public LibGit2Sharp.Commit CreateSquashCommitFromBranchOntoBranch(string branchToSquashName, string targetBranchName, string message)
    {
        var branchToSquash = LocalRepository.Branches[branchToSquashName];
        var targetBranch = LocalRepository.Branches[targetBranchName];

        if (branchToSquash is null)
        {
            throw new ArgumentException($"Branch '{branchToSquashName}' does not exist", nameof(branchToSquashName));
        }

        if (targetBranch is null)
        {
            throw new ArgumentException($"Target branch '{targetBranchName}' does not exist", nameof(targetBranchName));
        }

        var signature = new Signature(Some.Name(), Some.Email(), DateTimeOffset.Now);
        var parent = targetBranch.Tip;
        var tree = branchToSquash.Tip.Tree; // Use the full tree of the branch being squashed

        var squashCommit = LocalRepository.ObjectDatabase.CreateCommit(
            signature,
            signature,
            message,
            tree,
            new[] { parent },
            false);

        // Fast-forward the local target branch to the squash commit
        LocalRepository.Refs.UpdateTarget(targetBranch.Reference, squashCommit.Id);

        // Fast-forward the remote tracking branch if it exists
        if (targetBranch.TrackedBranch is not null)
        {
            LocalRepository.Refs.UpdateTarget(targetBranch.TrackedBranch.Reference, squashCommit.Id);
        }

        return squashCommit;
    }

    public Worktree CreateWorktree(string branchName)
    {
        // Validate branch exists
        if (LocalRepository.Branches[branchName] is null)
        {
            throw new ArgumentException($"Branch '{branchName}' does not exist.", nameof(branchName));
        }

        // Create a temporary directory for the worktree
        var worktreePath = TemporaryDirectory.CreatePath();
        var worktreeName = Some.Name();
        var worktree = LocalRepository.Worktrees.Add(branchName, worktreeName, worktreePath, false);

        WorktreePaths.Add(new TemporaryDirectory(worktreePath));

        return worktree;
    }

    public void CommitInWorktree(Worktree worktree, string relativePath, string contents, string? message = null)
    {
        var fullPath = Path.Combine(worktree.WorktreeRepository.Info.WorkingDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, contents);

        var wtRepo = worktree.WorktreeRepository;
        LibGit2Sharp.Commands.Stage(wtRepo, relativePath);
        var sig = new Signature(Some.Name(), Some.Name(), DateTimeOffset.Now);
        wtRepo.Commit(message ?? Some.Name(), sig, sig);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        LocalRepository.Dispose();
        LocalDirectory.Dispose();
        RemoteDirectory.Dispose();
        foreach (var worktreePath in WorktreePaths)
        {
            worktreePath.Dispose();
        }
    }
}
