
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Octopus.Shellfish;
using Spectre.Console;
using Spectre.Console.Cli;


var app = new CommandApp();
app.Configure(configure =>
{
    configure.AddCommand<NewStackCommand>("new").WithDescription("Creates a new stack.");
    configure.AddCommand<DeleteStackCommand>("delete").WithDescription("Deletes a stack.");
    configure.AddCommand<ListStacksCommand>("list").WithDescription("Lists all stacks.");
    configure.AddCommand<BranchCommand>("branch").WithDescription("Creates a new branch in a stack.");
    configure.AddCommand<UpdateStackCommand>("update").WithDescription("Updates the branches in a stack.");
});

await app.RunAsync(args);

class NewStackCommandSettings : CommandSettings
{
    [Description("The name of the stack. Must be unique.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }

    [Description("The source branch to use for the new branch. Defaults to the default branch for the repository.")]
    [CommandOption("-s|--source-branch")]
    public string? SourceBranch { get; init; }

    [Description("The name of the branch to create within the stack.")]
    [CommandOption("-b|--branch")]
    public string? BranchName { get; init; }
}

class NewStackCommand : AsyncCommand<NewStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, NewStackCommandSettings settings)
    {
        await Task.CompletedTask;

        var defaultBranch = GitOperations.GetDefaultBranch();
        var currentBranch = GitOperations.GetCurrentBranch();
        var branches = GitOperations.GetBranches();

        var name = settings.Name ?? AnsiConsole.Prompt(new TextPrompt<string>("Stack name:"));

        var branchesPrompt = new SelectionPrompt<string>().Title("Select a branch to start your stack from:").PageSize(10);

        if (branches.Contains(currentBranch, StringComparer.OrdinalIgnoreCase))
        {
            branchesPrompt.AddChoice(currentBranch);
        }
        branchesPrompt.AddChoice(defaultBranch);
        branchesPrompt.AddChoices(branches.Where(b => !b.Equals(currentBranch, StringComparison.OrdinalIgnoreCase) && !b.Equals(defaultBranch, StringComparison.OrdinalIgnoreCase)));

        var sourceBranch = settings.SourceBranch ?? AnsiConsole.Prompt(branchesPrompt);
        AnsiConsole.WriteLine($"Source branch: {sourceBranch}");

        var branchName = settings.BranchName ?? AnsiConsole.Prompt(new TextPrompt<string>("Branch name:"));
        GitOperations.CreateNewBranch(branchName, sourceBranch);
        GitOperations.PushNewBranch(branchName);

        var stacks = StackStorage.LoadStacks();

        var remoteUri = GitOperations.GetRemoteUri();

        stacks.Add(new Stack(name, remoteUri, sourceBranch, [branchName]));
        StackStorage.SaveStacks(stacks);

        AnsiConsole.WriteLine($"Stack '{name}' created from source branch '{sourceBranch}' with new branch '{branchName}'");

        if (AnsiConsole.Prompt(new ConfirmationPrompt("Do you want to switch to the new branch?")))
        {
            GitOperations.ChangeBranch(branchName);
        }

        return 0;
    }
}

class DeleteStackCommandSettings : CommandSettings
{
    [Description("The name of the stack to delete.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

class DeleteStackCommand : AsyncCommand<DeleteStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DeleteStackCommandSettings settings)
    {
        await Task.CompletedTask;

        var stacks = StackStorage.LoadStacks();

        var remoteUri = GitOperations.GetRemoteUri();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            AnsiConsole.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var stackSelection = settings.Name ?? AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Select a stack to delete:").PageSize(10).AddChoices(stacksForRemote.Select(s => s.Name).ToArray()));
        var stack = stacksForRemote.First(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        if (AnsiConsole.Prompt(new ConfirmationPrompt("Are you sure you want to delete this stack?")))
        {
            stacks.Remove(stack);
            StackStorage.SaveStacks(stacks);
            AnsiConsole.WriteLine($"Stack deleted");
        }

        return 0;
    }
}

class BranchCommandSettings : CommandSettings
{
    [Description("The name of the stack to create the branch in.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("The name of the branch to create.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

class BranchCommand : AsyncCommand<BranchCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BranchCommandSettings settings)
    {
        await Task.CompletedTask;

        var defaultBranch = GitOperations.GetDefaultBranch();
        var remoteUri = GitOperations.GetRemoteUri();
        var currentBranch = GitOperations.GetCurrentBranch();

        var stacks = StackStorage.LoadStacks();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            AnsiConsole.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var stackSelection = settings.Stack ?? AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Select a stack:").PageSize(10).AddChoices(stacksForRemote.Select(s => s.Name).ToArray()));
        var stack = stacksForRemote.First(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        var sourceBranch = stack.Branches.LastOrDefault() ?? stack.SourceBranch;

        var branchName = settings.Name ?? AnsiConsole.Prompt(new TextPrompt<string>("Branch name:"));

        AnsiConsole.WriteLine($"Creating branch '{branchName}' from '{sourceBranch}' in stack '{stack.Name}'");

        GitOperations.CreateNewBranch(branchName, sourceBranch);
        GitOperations.PushNewBranch(branchName);

        stack.Branches.Add(branchName);

        StackStorage.SaveStacks(stacks);

        AnsiConsole.WriteLine($"Branch created");
        return 0;
    }
}

class UpdateStackCommandSettings : CommandSettings
{
    [Description("The name of the stack to update.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

class UpdateStackCommand : AsyncCommand<UpdateStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, UpdateStackCommandSettings settings)
    {
        await Task.CompletedTask;

        var stacks = StackStorage.LoadStacks();

        var remoteUri = GitOperations.GetRemoteUri();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            AnsiConsole.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var stackSelection = settings.Name ?? AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Select a stack to update:").PageSize(10).AddChoices(stacksForRemote.Select(s => s.Name).ToArray()));
        var stack = stacksForRemote.First(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));
        var currentBranch = GitOperations.GetCurrentBranch();

        if (AnsiConsole.Prompt(new ConfirmationPrompt("Are you sure you want to update the branches in this stack?")))
        {
            void MergeFromSourceBranch(string branch, string sourceBranchName)
            {
                AnsiConsole.WriteLine($"Merging {sourceBranchName} into {branch}");

                GitOperations.ExecuteGitCommand($"fetch origin {sourceBranchName}");
                GitOperations.ExecuteGitCommand($"checkout {branch}");
                GitOperations.ExecuteGitCommand($"merge origin/{sourceBranchName}");
                GitOperations.ExecuteGitCommand($"push origin {branch}");
            }

            var sourceBranch = stack.SourceBranch;

            foreach (var branch in stack.Branches)
            {
                MergeFromSourceBranch(branch, sourceBranch);
                sourceBranch = branch;
            }

            if (stack.SourceBranch.Equals(currentBranch, StringComparison.InvariantCultureIgnoreCase) ||
                stack.Branches.Contains(currentBranch, StringComparer.OrdinalIgnoreCase))
            {
                GitOperations.ChangeBranch(currentBranch);
            }
        }

        return 0;
    }
}

static class GitOperations
{
    public static void CreateNewBranch(string branchName, string sourceBranch)
    {
        ExecuteGitCommand($"branch {branchName} {sourceBranch}");
    }

    public static void PushNewBranch(string branchName)
    {
        ExecuteGitCommand($"push -u origin {branchName}");
    }

    public static void ChangeBranch(string branchName)
    {
        ExecuteGitCommand($"checkout {branchName}");
    }

    public static string GetCurrentBranch()
    {
        return ExecuteGitCommandAndReturnOutput("branch --show-current").Trim();
    }

    public static string GetDefaultBranch()
    {
        return ExecuteGitCommandAndReturnOutput("symbolic-ref refs/remotes/origin/HEAD").Trim().Replace("refs/remotes/origin/", "");
    }

    public static string GetRemoteUri()
    {
        return ExecuteGitCommandAndReturnOutput("remote get-url origin").Trim();
    }

    public static string[] GetBranches()
    {
        return ExecuteGitCommandAndReturnOutput("branch --format=%(refname:short)").Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }

    public static string ExecuteGitCommandAndReturnOutput(string command)
    {
        var infoBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        var result = ShellExecutor.ExecuteCommand(
            "git",
            command,
            ".",
            (_) => { },
            (info) => infoBuilder.AppendLine(info),
            (error) => errorBuilder.AppendLine(error));

        if (result != 0)
        {
            AnsiConsole.MarkupLine($"[red]{errorBuilder}[/]");
            throw new Exception("Failed to execute git command.");
        }

        return infoBuilder.ToString();
    }

    public static void ExecuteGitCommand(string command)
    {
        var infoBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        AnsiConsole.MarkupLine($"[grey]git {command}[/]");

        var result = ShellExecutor.ExecuteCommand(
            "git",
            command,
            ".",
            (_) => { },
            (info) => infoBuilder.AppendLine(info),
            (error) => errorBuilder.AppendLine(error));

        if (result != 0)
        {
            AnsiConsole.MarkupLine($"[red]{errorBuilder}[/]");
            throw new Exception("Failed to execute git command.");
        }
        else
        {
            if (infoBuilder.Length > 0)
            {
                AnsiConsole.WriteLine(infoBuilder.ToString());
            }
        }
    }
}

static class StackStorage
{
    public static List<Stack> LoadStacks()
    {
        if (!File.Exists(@"D:\src\geofflamrock\stack\.stacks.json"))
        {
            return new List<Stack>();
        }
        var jsonString = File.ReadAllText(@"D:\src\geofflamrock\stack\.stacks.json");
        return JsonSerializer.Deserialize<List<Stack>>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    public static void SaveStacks(List<Stack> stacks)
    {
        File.WriteAllText(@"D:\src\geofflamrock\stack\.stacks.json", JsonSerializer.Serialize(stacks, new JsonSerializerOptions { WriteIndented = true }));
    }
}

class ListStacksCommand : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        await Task.CompletedTask;
        var stacks = StackStorage.LoadStacks();

        var remoteUri = GitOperations.GetRemoteUri();

        if (remoteUri is null)
        {
            AnsiConsole.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            AnsiConsole.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var currentBranch = GitOperations.GetCurrentBranch();

        foreach (var stack in stacksForRemote)
        {
            var stackRoot = new Tree($"[yellow]{stack.Name}[/]");
            var node = stackRoot.AddNode($"[grey]{stack.SourceBranch}[/]");

            // TreeNode AddChildren(TreeNode node, string branch)
            // {
            //     var branchName = branch.Name;
            //     if (branchName.Equals(currentBranch, StringComparison.OrdinalIgnoreCase))
            //     {
            //         branchName = $"[blue]{branchName} *[/]";
            //     }

            //     var childNode = node.AddNode(branchName);
            //     foreach (var childBranch in branch.Branches)
            //     {
            //         AddChildren(childNode, childBranch);
            //     }
            //     return childNode;
            // }

            foreach (var branch in stack.Branches)
            {
                var branchName = branch;
                if (branchName.Equals(currentBranch, StringComparison.OrdinalIgnoreCase))
                {
                    branchName = $"[blue]{branchName} *[/]";
                }

                node = node.AddNode(branchName);

                // AddChildren(sourceBranchNode, branch);
            }

            AnsiConsole.Write(stackRoot);
        }

        return 0;
    }
}

public static class ProcessHelpers
{
    public static string RunProcess(string fileName, string arguments)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processStartInfo };
        process.Start();
        string result = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return result;
    }
}



record Stack(string Name, string RemoteUri, string SourceBranch, List<string> Branches);