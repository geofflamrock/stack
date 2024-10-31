
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
    configure.SetApplicationName("stack");
    configure.UseAssemblyInformationalVersion();
    configure.AddCommand<NewStackCommand>("new").WithDescription("Creates a new stack.");
    configure.AddCommand<DeleteStackCommand>("delete").WithDescription("Deletes a stack.");
    configure.AddCommand<ListStacksCommand>("list").WithDescription("Lists all stacks.");
    configure.AddBranch("branch", branch =>
    {
        branch.SetDescription("Manages branches in a stack.");
        branch.SetDefaultCommand<BranchCommand>();
        branch.AddCommand<NewBranchCommand>("new").WithDescription("Creates a new branch in a stack.");
        branch.AddCommand<AddBranchCommand>("add").WithDescription("Adds an existing branch in a stack.");
    });
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
        var branches = GitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate();

        var name = settings.Name ?? AnsiConsole.Prompt(new TextPrompt<string>("Stack name:"));

        var branchesPrompt = new SelectionPrompt<string>().Title("Select a branch to start your stack from:").PageSize(10);

        branchesPrompt.AddChoices(branches);

        var sourceBranch = settings.SourceBranch ?? AnsiConsole.Prompt(branchesPrompt);
        AnsiConsole.WriteLine($"Source branch: {sourceBranch}");

        var stacks = StackStorage.LoadStacks();
        var remoteUri = GitOperations.GetRemoteUri();
        stacks.Add(new Stack(name, remoteUri, sourceBranch, []));

        StackStorage.SaveStacks(stacks);

        AnsiConsole.WriteLine($"Stack '{name}' created from source branch '{sourceBranch}'");

        if (AnsiConsole.Prompt(new ConfirmationPrompt("Do you want to add an existing branch or create a new branch and add it to the stack?")))
        {
            return await new BranchCommand().ExecuteAsync(context, new BranchCommandSettings { Stack = name });
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

    [Description("The name of the branch to add or create.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }

    [Description("The action to take.")]
    [CommandOption("-a|--action")]
    public BranchAction? Action { get; init; }
}

enum BranchAction
{
    [Description("Add an existing branch")]
    Add,

    [Description("Create a new branch")]
    Create
}

class BranchCommand : AsyncCommand<BranchCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BranchCommandSettings settings)
    {
        await Task.CompletedTask;

        var defaultBranch = GitOperations.GetDefaultBranch();
        var remoteUri = GitOperations.GetRemoteUri();
        var currentBranch = GitOperations.GetCurrentBranch();
        var branches = GitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate();

        var stacks = StackStorage.LoadStacks();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            AnsiConsole.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var stackSelection = settings.Stack ?? AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Select a stack:").PageSize(10).AddChoices(stacksForRemote.Select(s => s.Name).ToArray()));
        var stack = stacksForRemote.First(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        var actionPromptOption = new SelectionPrompt<BranchAction>()
            .Title("Add or create a branch:")
            .AddChoices([BranchAction.Add, BranchAction.Create]);

        actionPromptOption.Converter = (action) =>
        {
            var field = action.GetType().GetField(action.ToString());
            if (field == null)
                return action.ToString();

            var attributes = field.GetCustomAttributes(typeof(DescriptionAttribute), false);
            if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attribute)
            {
                return attribute.Description;
            }

            return action.ToString();
        };
        var action = AnsiConsole.Prompt(actionPromptOption);

        if (action == BranchAction.Add)
        {
            return await new AddBranchCommand().ExecuteAsync(context, new AddBranchCommandSettings { Stack = stack.Name, Name = settings.Name });
        }
        else
        {
            return await new NewBranchCommand().ExecuteAsync(context, new NewBranchCommandSettings { Stack = stack.Name, Name = settings.Name });
        }
    }
}

class NewBranchCommandSettings : CommandSettings
{
    [Description("The name of the stack to create the branch in.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("The name of the branch to create.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

class NewBranchCommand : AsyncCommand<NewBranchCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, NewBranchCommandSettings settings)
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

class AddBranchCommandSettings : CommandSettings
{
    [Description("The name of the stack to create the branch in.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("The name of the branch to add.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

class AddBranchCommand : AsyncCommand<AddBranchCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AddBranchCommandSettings settings)
    {
        await Task.CompletedTask;

        var defaultBranch = GitOperations.GetDefaultBranch();
        var remoteUri = GitOperations.GetRemoteUri();
        var currentBranch = GitOperations.GetCurrentBranch();
        var branches = GitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate();

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

        var branchesPrompt = new SelectionPrompt<string>().Title("Select a branch to add to the stack:").PageSize(10);

        // if (branches.Contains(currentBranch, StringComparer.OrdinalIgnoreCase))
        // {
        //     branchesPrompt.AddChoice(currentBranch);
        // }
        // if (currentBranch != defaultBranch)
        // {
        //     branchesPrompt.AddChoice(defaultBranch);
        // }
        // branchesPrompt.AddChoices(branches.Where(b => !b.Equals(currentBranch, StringComparison.OrdinalIgnoreCase) && !b.Equals(defaultBranch, StringComparison.OrdinalIgnoreCase)));
        branchesPrompt.AddChoices(branches);

        var branchName = settings.Name ?? AnsiConsole.Prompt(branchesPrompt);

        AnsiConsole.WriteLine($"Adding branch '{branchName}' to stack '{stack.Name}'");

        stack.Branches.Add(branchName);

        StackStorage.SaveStacks(stacks);

        AnsiConsole.WriteLine($"Branch added");
        return 0;
    }
}

class UpdateStackCommandSettings : CommandSettings
{
    [Description("The name of the stack to update.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }

    [Description("Show what would happen without making any changes.")]
    [CommandOption("--what-if")]
    [DefaultValue(false)]
    public bool WhatIf { get; init; }
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

        AnsiConsole.MarkupLine($"Stack: {stack.Name}");

        if (AnsiConsole.Prompt(new ConfirmationPrompt("Are you sure you want to update the branches in this stack?")))
        {
            void MergeFromSourceBranch(string branch, string sourceBranchName)
            {
                AnsiConsole.MarkupLine($"Merging [blue]{sourceBranchName}[/] into [blue]{branch}[/]");

                GitOperations.UpdateBranch(sourceBranchName, settings.WhatIf);
                GitOperations.UpdateBranch(branch, settings.WhatIf);
                GitOperations.MergeFromLocalSourceBranch(sourceBranchName, settings.WhatIf);
                GitOperations.PushBranch(branch, settings.WhatIf);
            }

            var sourceBranch = stack.SourceBranch;

            foreach (var branch in stack.Branches)
            {
                if (GitOperations.DoesRemoteBranchExist(branch))
                {
                    MergeFromSourceBranch(branch, sourceBranch);
                    sourceBranch = branch;
                }
                else
                {
                    // Remote branch no longer exists, skip over
                    AnsiConsole.MarkupLine($"[red]Branch '{branch}' no longer exists on the remote repository. Skipping...[/]");
                }
            }

            if (stack.SourceBranch.Equals(currentBranch, StringComparison.InvariantCultureIgnoreCase) ||
                stack.Branches.Contains(currentBranch, StringComparer.OrdinalIgnoreCase))
            {
                GitOperations.ChangeBranch(currentBranch, settings.WhatIf);
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

    public static void PushBranch(string branchName, bool whatIf = false)
    {
        ExecuteGitCommand($"push origin {branchName}", whatIf);
    }

    public static void ChangeBranch(string branchName, bool whatIf = false)
    {
        ExecuteGitCommand($"checkout {branchName}", whatIf);
    }

    public static void FetchBranch(string branchName, bool whatIf = false)
    {
        var currentBranch = GetCurrentBranch();

        if (currentBranch.Equals(branchName, StringComparison.OrdinalIgnoreCase))
        {
            ExecuteGitCommand($"fetch origin {branchName}", whatIf);
        }
        else
        {
            ExecuteGitCommand($"fetch origin {branchName}:{branchName}", whatIf);
        }
    }

    public static void Fetch()
    {
        ExecuteGitCommand("fetch");
    }

    public static void PullBranch(string branchName, bool whatIf = false)
    {
        ExecuteGitCommand($"pull origin {branchName}", whatIf);
    }

    public static void UpdateBranch(string branchName, bool whatIf = false)
    {
        var currentBranch = GetCurrentBranch();

        if (!currentBranch.Equals(branchName, StringComparison.OrdinalIgnoreCase))
        {
            ChangeBranch(branchName, whatIf);
        }

        PullBranch(branchName, whatIf);
    }

    public static void MergeFromRemoteSourceBranch(string sourceBranchName, bool whatIf = false)
    {
        ExecuteGitCommand($"merge origin/{sourceBranchName}", whatIf);
    }

    public static void MergeFromLocalSourceBranch(string sourceBranchName, bool whatIf = false)
    {
        ExecuteGitCommand($"merge {sourceBranchName}", whatIf);
    }

    public static string GetCurrentBranch()
    {
        return ExecuteGitCommandAndReturnOutput("branch --show-current").Trim();
    }

    public static string GetDefaultBranch()
    {
        return ExecuteGitCommandAndReturnOutput("symbolic-ref refs/remotes/origin/HEAD").Trim().Replace("refs/remotes/origin/", "");
    }

    public static bool DoesRemoteBranchExist(string branchName)
    {
        return ExecuteGitCommandAndReturnOutput($"ls-remote --heads origin {branchName}").Trim().Length > 0;
    }

    public static (int Ahead, int Behind) GetStatusOfBranch(string branchName, string sourceBranchName)
    {
        var status = ExecuteGitCommandAndReturnOutput($"rev-list --left-right --count {branchName}...{sourceBranchName}").Trim();
        var parts = status.Split('\t');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }

    public static string GetRemoteUri()
    {
        return ExecuteGitCommandAndReturnOutput("remote get-url origin").Trim();
    }

    public static string[] GetLocalBranchesOrderedByMostRecentCommitterDate()
    {
        return ExecuteGitCommandAndReturnOutput("branch --format=%(refname:short) --sort=-committerdate").Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }

    public static string ExecuteGitCommandAndReturnOutput(string command)
    {
        // AnsiConsole.MarkupLine($"[grey]git {command}[/]");

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

    public static void ExecuteGitCommand(string command, bool whatIf = false)
    {
        // AnsiConsole.MarkupLine($"[grey]git {command}[/]");

        if (!whatIf)
        {
            ExecuteGitCommandInternal(command);
        }
    }

    private static void ExecuteGitCommandInternal(string command)
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
    public static string GetStacksFile()
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(homeDirectory, ".stacks.json");
    }

    public static List<Stack> LoadStacks()
    {
        var stacksFile = GetStacksFile();
        if (!File.Exists(stacksFile))
        {
            return new List<Stack>();
        }
        var jsonString = File.ReadAllText(stacksFile);
        return JsonSerializer.Deserialize<List<Stack>>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    public static void SaveStacks(List<Stack> stacks)
    {
        var stacksFile = GetStacksFile();
        File.WriteAllText(stacksFile, JsonSerializer.Serialize(stacks, new JsonSerializerOptions { WriteIndented = true }));
    }
}

class ListStacksCommandSettings : CommandSettings
{
    [Description("Show the status of the branches against the remote in the stack.")]
    [CommandOption("--status")]
    [DefaultValue(false)]
    public bool CheckRemoteStatus { get; init; }
}

class ListStacksCommand : AsyncCommand<ListStacksCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ListStacksCommandSettings settings)
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

        var remoteStatusForBranchesInStacks = new Dictionary<string, BranchStatus>();

        if (settings.CheckRemoteStatus)
        {
            AnsiConsole.Status()
                .Start("Checking status of remote branches...", ctx =>
                {
                    GitOperations.Fetch();

                    foreach (var stack in stacksForRemote)
                    {
                        void CheckRemoteBranch(string branch, string sourceBranch)
                        {
                            var (ahead, behind) = GitOperations.GetStatusOfBranch(branch, sourceBranch);
                            var branchStatus = new BranchStatus(true, ahead, behind);
                            remoteStatusForBranchesInStacks[branch] = branchStatus;
                        }

                        var parentBranch = stack.SourceBranch;

                        foreach (var branch in stack.Branches)
                        {
                            if (GitOperations.DoesRemoteBranchExist(branch))
                            {
                                CheckRemoteBranch(branch, parentBranch);
                                parentBranch = branch;
                            }
                            else
                            {
                                remoteStatusForBranchesInStacks[branch] = new BranchStatus(false, 0, 0);
                            }
                        }
                    }
                });
        }


        var currentBranch = GitOperations.GetCurrentBranch();

        foreach (var stack in stacksForRemote)
        {
            var stackRoot = new Tree($"[yellow]{stack.Name}[/]");
            var sourceBranchNode = stackRoot.AddNode(BuildBranchName(stack.SourceBranch, true));

            string BuildBranchName(string branch, bool isSource)
            {
                var status = remoteStatusForBranchesInStacks.GetValueOrDefault(branch);
                var branchNameBuilder = new StringBuilder();

                var color = status?.ExistsInRemote == false ? "grey" : isSource ? "grey" : branch.Equals(currentBranch, StringComparison.OrdinalIgnoreCase) ? "blue" : null;
                Decoration? decoration = status?.ExistsInRemote == false ? Decoration.Strikethrough : null;

                if (color is not null && decoration is not null)
                {
                    branchNameBuilder.Append($"[{decoration} {color}]{branch}[/]");
                }
                else if (color is not null)
                {
                    branchNameBuilder.Append($"[{color}]{branch}[/]");
                }
                else if (decoration is not null)
                {
                    branchNameBuilder.Append($"[{decoration}]{branch}[/]");
                }
                else
                {
                    branchNameBuilder.Append(branch);
                }

                if (status?.ExistsInRemote == false)
                {
                    branchNameBuilder.Append($" [{Decoration.Strikethrough} grey](deleted in remote)[/]");
                }

                if (status?.Ahead > 0 && status?.Behind > 0)
                {
                    branchNameBuilder.Append($" [grey]({status.Ahead} ahead, {status.Behind} behind)[/]");
                }
                else if (status?.Ahead > 0)
                {
                    branchNameBuilder.Append($" [grey]({status.Ahead} ahead)[/]");
                }
                else if (status?.Behind > 0)
                {
                    branchNameBuilder.Append($" [grey]({status.Behind} behind)[/]");
                }

                return branchNameBuilder.ToString();
            }

            foreach (var branch in stack.Branches)
            {
                sourceBranchNode.AddNode(BuildBranchName(branch, false));
            }

            AnsiConsole.Write(stackRoot);
        }

        return 0;
    }
}

record BranchStatus(bool ExistsInRemote, int Ahead, int Behind);

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