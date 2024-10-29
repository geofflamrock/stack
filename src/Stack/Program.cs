
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Octopus.Shellfish;
using Spectre.Console;
using Spectre.Console.Cli;


var app = new CommandApp();
app.Configure(configure =>
{
    configure.AddCommand<NewStackCommand>("new").WithDescription("Creates a new stack.");
    configure.AddCommand<ListStacksCommand>("list").WithDescription("Lists all stacks.");
    configure.AddCommand<BranchCommand>("branch").WithDescription("Creates a new branch in a stack.");
});

await app.RunAsync(args);

// var remoteUri = RunProcess("git", "remote get-url origin").Trim();

// var jsonString = File.ReadAllText(@"D:\src\geofflamrock\stack\src\Stack\.stacks.json");
// var stacks = JsonSerializer.Deserialize<Stack[]>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
// var currentStack = stacks.FirstOrDefault(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase));

// if (currentStack is null)
// {
//     Console.WriteLine("No stack found for current repository.");
//     return;
// }

// var newBranchName = "geoffl/testing";
// RunProcess("git", $"checkout -b {newBranchName} {currentStack.SourceBranch}");
// RunProcess("git", $"push -u origin {newBranchName}");

// currentStack.Branches.Add(newBranchName);
// File.WriteAllText(@"D:\src\geofflamrock\stack\src\Stack\.stacks.json", JsonSerializer.Serialize(stacks, new JsonSerializerOptions { WriteIndented = true }));

// foreach (var stack in stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)))
// {
//     Console.WriteLine($"Name: {stack.Name}, RemoteUri: {stack.RemoteUri}, SourceBranch: {stack.SourceBranch}");
//     Console.WriteLine("Branches: " + string.Join(", ", stack.Branches));
// }
class NewStackCommandSettings : CommandSettings
{
    [Description("The name of the stack. Must be unique.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }

    [Description("The source branch to use for the new branch. Defaults to the default branch for the repository.")]
    [CommandOption("-s|--source-branch")]
    public string? SourceBranch { get; init; }
}

class NewStackCommand : AsyncCommand<NewStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, NewStackCommandSettings settings)
    {
        await Task.CompletedTask;

        var defaultBranch = ProcessHelpers.RunProcess("git", "symbolic-ref refs/remotes/origin/HEAD").Trim().Replace("refs/remotes/origin/", "");

        var name = settings.Name ?? AnsiConsole.Prompt(new TextPrompt<string>("Stack name:"));
        var sourceBranch = settings.SourceBranch ?? AnsiConsole.Prompt(
            new TextPrompt<string>("Which branch would you like to start your stack from? Leave empty to use the default branch.")
                .DefaultValue(defaultBranch));

        var jsonString = File.ReadAllText(@"D:\src\geofflamrock\stack\src\Stack\.stacks.json");
        var stacks = JsonSerializer.Deserialize<List<Stack>>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        var remoteUri = ProcessHelpers.RunProcess("git", "remote get-url origin").Trim();

        stacks.Add(new Stack(name, remoteUri, sourceBranch, []));
        File.WriteAllText(@"D:\src\geofflamrock\stack\src\Stack\.stacks.json", JsonSerializer.Serialize(stacks, new JsonSerializerOptions { WriteIndented = true }));

        AnsiConsole.WriteLine($"Stack created");
        return 0;
    }
}

class BranchCommandSettings : CommandSettings
{
    [Description("The name of the stack to create the branch in.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("The name of the branch to create.")]
    [CommandOption("-b|--branch")]
    public string? Branch { get; init; }
}

class BranchCommand : AsyncCommand<BranchCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BranchCommandSettings settings)
    {
        await Task.CompletedTask;

        var defaultBranch = ProcessHelpers.RunProcess("git", "symbolic-ref refs/remotes/origin/HEAD").Trim().Replace("refs/remotes/origin/", "");

        var jsonString = File.ReadAllText(@"D:\src\geofflamrock\stack\src\Stack\.stacks.json");
        var stacks = JsonSerializer.Deserialize<List<Stack>>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        var remoteUri = ProcessHelpers.RunProcess("git", "remote get-url origin").Trim();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            AnsiConsole.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var stackSelection = settings.Stack ?? AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Select a stack:").PageSize(10).AddChoices(stacksForRemote.Select(s => s.Name).ToArray()));
        var stack = stacksForRemote.First(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        var sourceBranchPrompt = new SelectionPrompt<string>().Title("Select a branch to create from:").PageSize(10);

        sourceBranchPrompt.AddChoice($"[grey]{stack.SourceBranch}[/]");

        foreach (var branch in stack.Branches)
        {
            sourceBranchPrompt.AddChoice(branch.Name);
        }

        var sourceBranch = AnsiConsole.Prompt(sourceBranchPrompt);

        // var sourceBranch = stack.Branches.Count > 0 ? AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Select a branch to create from:")
        //     .PageSize(10)
        //     .AddChoices(new List<string>([stack.SourceBranch]).Concat(stack.Branches.Select(b => b.Name).ToArray()))) : stack.SourceBranch;

        var branchName = settings.Branch ?? AnsiConsole.Prompt(new TextPrompt<string>("Branch name:"));

        AnsiConsole.WriteLine($"Creating branch {branchName} from {sourceBranch} in {stack.Name}");

        ShellExecutor.ExecuteCommand(
            "git",
            $"checkout -b {branchName} {sourceBranch}",
            ".",
            (_) => { },
            AnsiConsole.WriteLine,
            (error) => AnsiConsole.MarkupLine($"[red]{error}[/]"));

        // AnsiConsole.WriteLine(ProcessHelpers.RunProcess("git", $"checkout -b {branchName} {sourceBranch}"));
        // ProcessHelpers.RunProcess("git", $"push -u origin {branchName}");

        stack.Branches.Add(new StackBranch(branchName, []));

        File.WriteAllText(@"D:\src\geofflamrock\stack\src\Stack\.stacks.json", JsonSerializer.Serialize(stacks, new JsonSerializerOptions { WriteIndented = true }));

        AnsiConsole.WriteLine($"Branch created");
        return 0;
    }
}

class ListStacksCommand : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        await Task.CompletedTask;
        var jsonString = File.ReadAllText(@"D:\src\geofflamrock\stack\src\Stack\.stacks.json");
        var stacks = JsonSerializer.Deserialize<List<Stack>>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        ShellExecutor.ExecuteCommand(
            "git",
            "remote get-url origin",
            ".",
            (_) => { },
            AnsiConsole.WriteLine,
            (error) => AnsiConsole.WriteLine($"[red]{error}[/]"));

        var remoteUri = ProcessHelpers.RunProcess("git", "remote get-url origin").Trim();

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

        var currentBranch = ProcessHelpers.RunProcess("git", "branch --show-current").Trim();

        foreach (var stack in stacksForRemote)
        {
            var stackRoot = new Tree(stack.Name);
            var sourceBranchNode = stackRoot.AddNode($"[grey]{stack.SourceBranch}[/]");

            TreeNode AddChildren(TreeNode node, StackBranch branch)
            {
                var branchName = branch.Name;
                if (branchName.Equals(currentBranch, StringComparison.OrdinalIgnoreCase))
                {
                    branchName = $"[blue]{branchName} *[/]";
                }

                var childNode = node.AddNode(branchName);
                foreach (var childBranch in branch.Branches)
                {
                    AddChildren(childNode, childBranch);
                }
                return childNode;
            }

            foreach (var branch in stack.Branches)
            {
                AddChildren(sourceBranchNode, branch);
            }

            // TreeNode? currentNode = null;
            // foreach (var branch in stack.Branches)
            // {
            //     var branchName = branch.Name;
            //     if (branchName.Equals(currentBranch, StringComparison.OrdinalIgnoreCase))
            //     {
            //         branchName = $"[blue]{branchName} *[/]";
            //     }
            //     currentNode = currentNode?.AddNode(branchName) ?? stackRoot.AddNode(branchName);
            // }

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



record Stack(string Name, string RemoteUri, string SourceBranch, List<StackBranch> Branches);

record StackBranch(string Name, List<StackBranch> Branches);