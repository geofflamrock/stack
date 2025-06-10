using Spectre.Console.Cli;
using Stack.Commands;
using Stack.Help;

var app = new CommandApp();
app.Configure(configure =>
{
    configure.SetApplicationName("stack");
    configure.SetHelpProvider(new StackHelpProvider(configure.Settings));
    configure.UseAssemblyInformationalVersion();

    // Stack commands
    configure.AddCommand<NewStackCommand>(CommandNames.New).WithDescription("Creates a new stack.");
    configure.AddCommand<ListStacksCommand>(CommandNames.List).WithDescription("List stacks.");
    configure.AddCommand<StackStatusCommand>(CommandNames.Status).WithDescription("Shows the status of a stack.");
    configure.AddCommand<DeleteStackCommand>(CommandNames.Delete).WithDescription("Deletes a stack.");

    // Branch commands
    configure.AddCommand<StackSwitchCommand>(CommandNames.Switch).WithDescription("Switches to a branch in a stack.");
    configure.AddCommand<UpdateStackCommand>(CommandNames.Update).WithDescription("Updates the branches in a stack.");
    configure.AddCommand<CleanupStackCommand>(CommandNames.Cleanup).WithDescription("Cleans up unused branches in a stack.");
    configure.AddBranch(CommandNames.Branch, branch =>
        {
            branch.SetDescription("Manages branches in a stack.");
            branch.AddCommand<NewBranchCommand>(CommandNames.New).WithDescription("Creates a new branch in a stack.");
            branch.AddCommand<AddBranchCommand>(CommandNames.Add).WithDescription("Adds an existing branch in a stack.");
            branch.AddCommand<RemoveBranchCommand>(CommandNames.Remove).WithDescription("Removes a branch from a stack.");
        });

    // Remote commands
    configure.AddCommand<PullStackCommand>(CommandNames.Pull).WithDescription("Pulls changes from the remote repository for a stack.");
    configure.AddCommand<PushStackCommand>(CommandNames.Push).WithDescription("Pushes changes to the remote repository for a stack.");
    configure.AddCommand<SyncStackCommand>(CommandNames.Sync).WithDescription("Syncs a stack with the remote repository. Shortcut for `git fetch --prune`, `stack pull`, `stack update` and `stack push`.");

    // GitHub commands
    configure.AddBranch(CommandNames.Pr, pr =>
        {
            pr.SetDescription("Manages pull requests for a stack.");
            pr.AddCommand<CreatePullRequestsCommand>(CommandNames.Create).WithDescription("Creates pull requests for a stack.");
            pr.AddCommand<OpenPullRequestsCommand>(CommandNames.Open).WithDescription("Opens pull requests for a stack in the default browser.");
            pr.AddCommand<SetPullRequestDescriptionCommand>(CommandNames.Description).WithDescription("Sets the pull request description for the stack and applies it all pull requests.");
        });

    // Advanced commands
    configure.AddBranch(CommandNames.Config, config =>
    {
        config.SetDescription("Manages stack configuration.");
        config.AddCommand<OpenConfigCommand>(CommandNames.Open).WithDescription("Opens the configuration file in the default editor.");
        config.AddCommand<MigrateConfigCommand>(CommandNames.Migrate).WithDescription("Migrates the configuration file from v1 to v2 format (preview).");
    });
});

await app.RunAsync(args);
