using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure.Settings;
using Stack.Commands;

namespace Stack.Infrastructure;

public static class ServiceConfiguration
{
    public static IHost CreateHost()
    {
        var settings = new HostApplicationBuilderSettings();
        var builder = Host.CreateEmptyApplicationBuilder(settings);
        ConfigureServices(builder.Services);
        return builder.Build();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Register memory cache
        services.AddMemoryCache();

        // Register console components
        services.AddSingleton(provider =>
        {
            return AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.Detect,
                ColorSystem = ColorSystemSupport.Detect,
                Out = new AnsiConsoleOutput(Console.Error),
            });
        });
        services.AddSingleton<IStdOutLogger, StdOutLogger>();
        services.AddSingleton<IStdErrLogger, StdErrLogger>();
        services.AddSingleton<ILogger>(provider => provider.GetRequiredService<IStdErrLogger>());
        services.AddSingleton<IInputProvider, ConsoleInputProvider>();

        // Register config
        services.AddSingleton<IStackConfig, FileStackConfig>();

        // Register file operations
        services.AddSingleton<IFileOperations, FileOperations>();

        services.AddSingleton<CliExecutionContext>();

        // Register Git and GitHub clients
        services.AddSingleton<IGitClient, GitClient>();
        services.AddSingleton<GitHubClient>();
        services.AddSingleton<IGitHubClient>(provider =>
        {
            var gitHubClient = provider.GetRequiredService<GitHubClient>();
            var cache = provider.GetRequiredService<IMemoryCache>();
            return new CachingGitHubClient(gitHubClient, cache);
        });

        // Register stack actions
        services.AddSingleton<IStackActions, StackActions>();

        // Register command handlers
        RegisterCommandHandlers(services);

        // Register commands 
        RegisterCommands(services);
    }

    private static void RegisterCommandHandlers(IServiceCollection services)
    {
        // Stack command handlers
        services.AddTransient<NewStackCommandHandler>();
        services.AddTransient<UpdateStackCommandHandler>();
        services.AddTransient<DeleteStackCommandHandler>();
        services.AddTransient<ListStacksCommandHandler>();
        services.AddTransient<CleanupStackCommandHandler>();
        services.AddTransient<StackStatusCommandHandler>();
        services.AddTransient<StackSwitchCommandHandler>();

        // Branch command handlers
        services.AddTransient<AddBranchCommandHandler>();
        services.AddTransient<NewBranchCommandHandler>();
        services.AddTransient<RemoveBranchCommandHandler>();

        // Remote operation handlers
        services.AddTransient<PullStackCommandHandler>();
        services.AddTransient<PushStackCommandHandler>();
        services.AddTransient<SyncStackCommandHandler>();

        // Pull request handlers
        services.AddTransient<CreatePullRequestsCommandHandler>();
        services.AddTransient<OpenPullRequestsCommandHandler>();
    }

    private static void RegisterCommands(IServiceCollection services)
    {
        // Root command
        services.AddSingleton<StackRootCommand>();

        // Individual commands
        services.AddTransient<NewStackCommand>();
        services.AddTransient<UpdateStackCommand>();
        services.AddTransient<DeleteStackCommand>();
        services.AddTransient<ListStacksCommand>();
        services.AddTransient<CleanupStackCommand>();
        services.AddTransient<StackStatusCommand>();
        services.AddTransient<StackSwitchCommand>();

        // Branch commands
        services.AddTransient<BranchCommand>();
        services.AddTransient<AddBranchCommand>();
        services.AddTransient<NewBranchCommand>();
        services.AddTransient<RemoveBranchCommand>();

        // Remote commands
        services.AddTransient<PullStackCommand>();
        services.AddTransient<PushStackCommand>();
        services.AddTransient<SyncStackCommand>();

        // Pull request commands
        services.AddTransient<PullRequestsCommand>();
        services.AddTransient<CreatePullRequestsCommand>();
        services.AddTransient<OpenPullRequestsCommand>();

        // Config commands
        services.AddTransient<ConfigCommand>();
        services.AddTransient<OpenConfigCommand>();
    }
}