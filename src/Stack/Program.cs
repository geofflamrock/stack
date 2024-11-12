
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands;
using Stack.Config;
using Stack.Git;
using Stack.Help;
using Stack.Infrastructure;

var services = new ServiceCollection();
services.AddSingleton(AnsiConsole.Console);
services.AddSingleton<IGitOperations, GitOperations>();
services.AddSingleton<IGitHubOperations, GitHubOperations>();
services.AddSingleton<IStackConfig, StackConfig>();

var app = new CommandApp(new ServiceCollectionTypeRegistrar(services));
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
});

await app.RunAsync(args);



