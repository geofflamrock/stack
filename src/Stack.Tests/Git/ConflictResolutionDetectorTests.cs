using System.Diagnostics;
using FluentAssertions;
using Meziantou.Extensions.Logging.Xunit;
using Microsoft.Extensions.Logging;
using Stack.Git;
using Stack.Tests.Helpers;
using Xunit.Abstractions;

namespace Stack.Tests.Git;

public class ConflictResolutionDetectorTests(ITestOutputHelper testOutputHelper)
{
    private ILogger CreateLogger<T>() => XUnitLogger.CreateLogger<T>(testOutputHelper);

    private static void RunGit(string workingDir, string args)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            throw new Exception($"git {args} failed: {p.StandardError.ReadToEnd()}");
        }
    }

    [Fact]
    public async Task WaitForConflictResolution_WhenNotStarted_ReturnsNotStarted()
    {
        using var repo = new TestGitRepositoryBuilder().Build();
        var logger = CreateLogger<ConflictResolutionDetectorTests>();
        var git = new GitClient(XUnitLogger.CreateLogger<GitClient>(testOutputHelper), repo.LocalDirectoryPath);

        var conflictResolutionDetector = new ConflictResolutionDetector();

        var result = await conflictResolutionDetector.WaitForConflictResolution(
            git,
            logger,
            ConflictOperationType.Merge,
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(200),
            CancellationToken.None);

        result.Should().Be(ConflictResolutionResult.NotStarted);
    }

    [Fact]
    public async Task WaitForConflictResolution_WhenMergeCompletes_ReturnsCompleted()
    {
        var branchBase = Some.BranchName();
        var branchOther = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b.WithName(branchBase))
            .WithBranch(b => b.WithName(branchOther).FromSourceBranch(branchBase))
            .Build();

        var logger = CreateLogger<ConflictResolutionDetectorTests>();
        var git = new GitClient(XUnitLogger.CreateLogger<GitClient>(testOutputHelper), repo.LocalDirectoryPath);

        var conflictResolutionDetector = new ConflictResolutionDetector();

        var relFile = Some.Name();
        var filePath = Path.Join(repo.LocalDirectoryPath, relFile);

        // create conflicting change on base
        git.ChangeBranch(branchBase);
        File.WriteAllText(filePath, "base");
        repo.Stage(relFile);
        repo.Commit();

        // different conflicting change on other branch
        git.ChangeBranch(branchOther);
        File.WriteAllText(filePath, "other");
        repo.Stage(relFile);
        repo.Commit();

        // start merge that will conflict
        git.ChangeBranch(branchBase);
        try { git.MergeFromLocalSourceBranch(branchOther); } catch (ConflictException) { }

        // small spin to ensure merge state file created before detector call
        var spinStart = DateTime.UtcNow;
        while (!git.IsMergeInProgress() && DateTime.UtcNow - spinStart < TimeSpan.FromMilliseconds(200))
        {
            await Task.Delay(10);
        }

        // Resolve conflict after a short delay
        var resolver = Task.Run(async () =>
        {
            await Task.Delay(250); // ensure detector captured initial head/in-progress
            File.WriteAllText(filePath, "resolved");
            RunGit(repo.LocalDirectoryPath, $"add {relFile}");
            RunGit(repo.LocalDirectoryPath, "commit -m resolved-merge");
        });

        var result = await conflictResolutionDetector.WaitForConflictResolution(
            git,
            logger,
            ConflictOperationType.Merge,
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromSeconds(2),
            CancellationToken.None);

        await resolver;
        result.Should().Be(ConflictResolutionResult.Completed);
    }

    [Fact]
    public async Task WaitForConflictResolution_WhenMergeAborted_ReturnsAborted()
    {
        var branchBase = Some.BranchName();
        var branchOther = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b.WithName(branchBase))
            .WithBranch(b => b.WithName(branchOther).FromSourceBranch(branchBase))
            .Build();

        var logger = CreateLogger<ConflictResolutionDetectorTests>();
        var git = new GitClient(XUnitLogger.CreateLogger<GitClient>(testOutputHelper), repo.LocalDirectoryPath);

        var relFile = Some.Name();
        var filePath = Path.Join(repo.LocalDirectoryPath, relFile);

        // conflicting commits
        git.ChangeBranch(branchBase);
        File.WriteAllText(filePath, "base");
        repo.Stage(relFile);
        repo.Commit();

        git.ChangeBranch(branchOther);
        File.WriteAllText(filePath, "other");
        repo.Stage(relFile);
        repo.Commit();

        git.ChangeBranch(branchBase);
        try { git.MergeFromLocalSourceBranch(branchOther); } catch (ConflictException) { }

        // Abort after delay
        var aborter = Task.Run(async () => { await Task.Delay(60); git.AbortMerge(); });

        var conflictResolutionDetector = new ConflictResolutionDetector();

        var result = await conflictResolutionDetector.WaitForConflictResolution(
            git,
            logger,
            ConflictOperationType.Merge,
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromSeconds(2),
            CancellationToken.None);

        await aborter;
        result.Should().Be(ConflictResolutionResult.Aborted);
    }

    [Fact]
    public async Task WaitForConflictResolution_WhenTimeoutReached_ReturnsTimeout()
    {
        var branchBase = Some.BranchName();
        var branchOther = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b.WithName(branchBase))
            .WithBranch(b => b.WithName(branchOther).FromSourceBranch(branchBase))
            .Build();

        var logger = CreateLogger<ConflictResolutionDetectorTests>();
        var git = new GitClient(XUnitLogger.CreateLogger<GitClient>(testOutputHelper), repo.LocalDirectoryPath);

        var relFile = Some.Name();
        var filePath = Path.Join(repo.LocalDirectoryPath, relFile);

        // conflicting commits
        git.ChangeBranch(branchBase);
        File.WriteAllText(filePath, "base");
        repo.Stage(relFile);
        repo.Commit();

        git.ChangeBranch(branchOther);
        File.WriteAllText(filePath, "other");
        repo.Stage(relFile);
        repo.Commit();

        git.ChangeBranch(branchBase);
        try { git.MergeFromLocalSourceBranch(branchOther); } catch (ConflictException) { }

        var conflictResolutionDetector = new ConflictResolutionDetector();

        // Do not resolve or abort; should timeout
        var result = await conflictResolutionDetector.WaitForConflictResolution(
            git,
            logger,
            ConflictOperationType.Merge,
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(150),
            CancellationToken.None);

        result.Should().Be(ConflictResolutionResult.Timeout);
    }

    [Fact]
    public async Task WaitForConflictResolution_WhenCancelled_Throws()
    {
        var branchBase = Some.BranchName();
        var branchOther = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b.WithName(branchBase))
            .WithBranch(b => b.WithName(branchOther).FromSourceBranch(branchBase))
            .Build();

        var logger = CreateLogger<ConflictResolutionDetectorTests>();
        var git = new GitClient(XUnitLogger.CreateLogger<GitClient>(testOutputHelper), repo.LocalDirectoryPath);

        var relFile = Some.Name();
        var filePath = Path.Join(repo.LocalDirectoryPath, relFile);

        // conflicting commits to start rebase later
        git.ChangeBranch(branchBase);
        File.WriteAllText(filePath, "base");
        repo.Stage(relFile);
        repo.Commit();

        git.ChangeBranch(branchOther);
        File.WriteAllText(filePath, "other");
        repo.Stage(relFile);
        repo.Commit();

        var conflictResolutionDetector = new ConflictResolutionDetector();

        // start rebase that will conflict
        try { git.RebaseFromLocalSourceBranch(branchBase); } catch (ConflictException) { }

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);
        var act = async () => await conflictResolutionDetector.WaitForConflictResolution(
            git,
            logger,
            ConflictOperationType.Rebase,
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromSeconds(2),
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WaitForConflictResolution_WhenRebaseCompletes_ReturnsCompleted()
    {
        var baseBranch = Some.BranchName();
        var featureBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b.WithName(baseBranch))
            .WithBranch(b => b.WithName(featureBranch).FromSourceBranch(baseBranch))
            .Build();

        var logger = CreateLogger<ConflictResolutionDetectorTests>();
        var git = new GitClient(XUnitLogger.CreateLogger<GitClient>(testOutputHelper), repo.LocalDirectoryPath);

        var relFile = Some.Name();
        var filePath = Path.Join(repo.LocalDirectoryPath, relFile);

        // conflicting commits
        git.ChangeBranch(baseBranch);
        File.WriteAllText(filePath, "base");
        repo.Stage(relFile);
        repo.Commit();

        git.ChangeBranch(featureBranch);
        File.WriteAllText(filePath, "feature");
        repo.Stage(relFile);
        repo.Commit();

        var conflictResolutionDetector = new ConflictResolutionDetector();

        git.ChangeBranch(featureBranch);
        try { git.RebaseFromLocalSourceBranch(baseBranch); } catch (ConflictException) { }

        // spin until rebase detected
        var spinStart = DateTime.UtcNow;
        while (!git.IsRebaseInProgress() && DateTime.UtcNow - spinStart < TimeSpan.FromMilliseconds(200))
        {
            await Task.Delay(10);
        }

        var resolver = Task.Run(async () =>
        {
            await Task.Delay(120);
            File.WriteAllText(filePath, "resolved");
            RunGit(repo.LocalDirectoryPath, $"add {relFile}");
            var start = DateTime.UtcNow;
            while (git.IsRebaseInProgress() && DateTime.UtcNow - start < TimeSpan.FromSeconds(3))
            {
                try
                {
                    // Provide inline editor config so git doesn't try to launch an interactive editor
                    RunGit(repo.LocalDirectoryPath, "-c core.editor=true rebase --continue");
                }
                catch (Exception)
                {
                    // If still conflicts (unlikely after we wrote resolved file) just wait and retry
                }
                await Task.Delay(40);
            }
        });

        var result = await conflictResolutionDetector.WaitForConflictResolution(
            git,
            logger,
            ConflictOperationType.Rebase,
            TimeSpan.FromMilliseconds(10),
            null,
            CancellationToken.None);

        await resolver;
        result.Should().Be(ConflictResolutionResult.Completed);
    }

    [Fact]
    public async Task WaitForConflictResolution_WhenRebaseAborted_ReturnsAborted()
    {
        var baseBranch = Some.BranchName();
        var featureBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b.WithName(baseBranch))
            .WithBranch(b => b.WithName(featureBranch).FromSourceBranch(baseBranch))
            .Build();

        var logger = CreateLogger<ConflictResolutionDetectorTests>();
        var git = new GitClient(XUnitLogger.CreateLogger<GitClient>(testOutputHelper), repo.LocalDirectoryPath);

        var relFile = Some.Name();
        var filePath = Path.Join(repo.LocalDirectoryPath, relFile);

        // conflicting commits
        git.ChangeBranch(baseBranch);
        File.WriteAllText(filePath, "base");
        repo.Stage(relFile);
        repo.Commit();

        git.ChangeBranch(featureBranch);
        File.WriteAllText(filePath, "feature");
        repo.Stage(relFile);
        repo.Commit();

        var conflictResolutionDetector = new ConflictResolutionDetector();

        git.ChangeBranch(featureBranch);
        try { git.RebaseFromLocalSourceBranch(baseBranch); } catch (ConflictException) { }

        var aborter = Task.Run(async () => { await Task.Delay(60); git.AbortRebase(); });

        var result = await conflictResolutionDetector.WaitForConflictResolution(
            git,
            logger,
            ConflictOperationType.Rebase,
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromSeconds(2),
            CancellationToken.None);

        await aborter;
        result.Should().Be(ConflictResolutionResult.Aborted);
    }
}