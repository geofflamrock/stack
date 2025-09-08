using System.CommandLine;

public static class CommonOptions
{
    public static Option<string?> WorkingDirectory { get; } = new Option<string?>("--working-dir")
    {
        Description = "The path to the directory containing the git repository. Defaults to the current directory.",
        Required = false
    };

    public static Option<bool> Verbose { get; } = new Option<bool>("--verbose")
    {
        Description = "Show verbose output.",
        Required = false
    };

    public static Option<bool> Json { get; } = new Option<bool>("--json")
    {
        Description = "Output results as JSON.",
        Required = false
    };

    public static Option<string?> Stack { get; } = new Option<string?>("--stack", "-s")
    {
        Description = "The name of the stack.",
        Required = false
    };

    public static Option<string?> Name { get; } = new Option<string?>("--name", "-n")
    {
        Description = "The new name for the stack.",
        Required = false
    };

    public static Option<int> MaxBatchSize { get; } = new Option<int>("--max-batch-size")
    {
        Description = "The maximum number of branches to process at once.",
        DefaultValueFactory = _ => 5
    };

    public static Option<bool?> Rebase { get; } = new Option<bool?>("--rebase")
    {
        Description = "Use rebase when updating the stack. Overrides any setting in Git configuration.",
        Required = false
    };

    public static Option<bool?> Merge { get; } = new Option<bool?>("--merge")
    {
        Description = "Use merge when updating the stack. Overrides any setting in Git configuration.",
        Required = false
    };

    public static Option<bool> Confirm { get; } = new Option<bool>("--yes", "-y")
    {
        Description = "Confirm the command without prompting.",
        Required = false
    };

    public static Option<string?> Branch { get; } = new Option<string?>("--branch", "-b")
    {
        Description = "The name of the branch.",
        Required = false
    };

    public static Option<string?> ParentBranch { get; } = new Option<string?>("--parent", "-p")
    {
        Description = "The name of the parent branch to put the branch under.",
        Required = false
    };
}