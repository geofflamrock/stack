
using Spectre.Console.Cli;
using Stack.Commands;

var app = new CommandApp();
app.Configure(configure =>
{
    configure.SetApplicationName("stack");
    configure.UseAssemblyInformationalVersion();
    configure.AddCommand<NewStackCommand>("new").WithDescription("Creates a new stack.");
    configure.AddCommand<ListStacksCommand>("list").WithDescription("Lists all stacks.");
    configure.AddCommand<StackStatusCommand>("status").WithDescription("Shows the status of a stack.");
    configure.AddCommand<DeleteStackCommand>("delete").WithDescription("Deletes a stack.");
    configure.AddCommand<UpdateStackCommand>("update").WithDescription("Updates the branches in a stack.");

    configure.AddCommand<OpenConfigCommand>("config").WithDescription("Opens the configuration file in the default editor.");

    configure.AddBranch("branch", branch =>
        {
            branch.SetDescription("Manages branches in a stack.");
            branch.SetDefaultCommand<BranchCommand>();
            branch.AddCommand<NewBranchCommand>("new").WithDescription("Creates a new branch in a stack.");
            branch.AddCommand<AddBranchCommand>("add").WithDescription("Adds an existing branch in a stack.");
        });

});

await app.RunAsync(args);



