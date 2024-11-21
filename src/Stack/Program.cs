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
        });

    // GitHub commands
    configure.AddBranch(CommandNames.Pr, pr =>
        {
            pr.SetDescription("Manages pull requests for a stack. [[EXPERIMENTAL]]");
            pr.AddCommand<CreatePullRequestsCommand>(CommandNames.Create).WithDescription("Creates pull requests for a stack.");
            pr.AddCommand<OpenPullRequestsCommand>(CommandNames.Open).WithDescription("Opens pull requests for a stack in the default browser.");
        });

    // Advanced commands
    configure.AddCommand<OpenConfigCommand>(CommandNames.Config).WithDescription("Opens the configuration file in the default editor.");
});

await app.RunAsync(args);
