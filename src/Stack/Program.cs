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
    configure.AddCommand<NewStackCommand>("new").WithDescription("Creates a new stack.");
    configure.AddCommand<ListStacksCommand>("list").WithDescription("List stacks.");
    configure.AddCommand<StackStatusCommand>("status").WithDescription("Shows the status of a stack.");
    configure.AddCommand<DeleteStackCommand>("delete").WithDescription("Deletes a stack.");

    // Branch commands
    configure.AddCommand<StackSwitchCommand>("switch").WithDescription("Switches to a branch in a stack.");
    configure.AddCommand<UpdateStackCommand>("update").WithDescription("Updates the branches in a stack.");
    configure.AddBranch("branch", branch =>
        {
            branch.SetDescription("Manages branches in a stack.");
            branch.AddCommand<NewBranchCommand>("new").WithDescription("Creates a new branch in a stack.");
            branch.AddCommand<AddBranchCommand>("add").WithDescription("Adds an existing branch in a stack.");
        });

    // Config commands
    configure.AddCommand<OpenConfigCommand>("config").WithDescription("Opens the configuration file in the default editor.");

    // Pull request commands
    configure.AddBranch("pr", pr =>
        {
            pr.SetDescription("Manages pull requests for a stack. [[EXPERIMENTAL]]");
            pr.AddCommand<CreatePullRequestsCommand>("create").WithDescription("Creates pull requests for a stack.");
            pr.AddCommand<OpenPullRequestsCommand>("open").WithDescription("Opens pull requests for a stack in the default browser.");
        });
});

await app.RunAsync(args);



