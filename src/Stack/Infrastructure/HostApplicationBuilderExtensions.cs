using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;
using Stack.Commands;
using Microsoft.Extensions.Logging;

namespace Stack.Infrastructure;

public static class HostApplicationBuilderExtensions
{
    public static IHostApplicationBuilder ConfigureServices(this IHostApplicationBuilder builder, string[] args)
    {
        builder.Services.ConfigureServices(args);
        return builder;
    }

    public static IHostApplicationBuilder ConfigureLogging(this IHostApplicationBuilder builder, string[] args)
    {
        builder.Logging.SetMinimumLevel(LogLevel.Debug);

        if (args.Contains("--verbose") || args.Contains("-v"))
        {
            builder.Logging.SetMinimumLevel(LogLevel.Trace);
        }

        builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ILoggerProvider, AnsiConsoleLoggerProvider>();

        return builder;
    }

    private static void ConfigureServices(this IServiceCollection services, string[] args)
    {
        services.AddMemoryCache();
        services.AddSingleton(provider =>
        {
            var stream = args.Contains("--json") ? Console.Error : Console.Out;

            return AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.Detect,
                ColorSystem = ColorSystemSupport.Detect,
                Out = new AnsiConsoleOutput(stream),
            });
        });
        services.AddSingleton<IDisplayProvider, ConsoleDisplayProvider>();
        services.AddSingleton<IInputProvider, ConsoleInputProvider>();
        services.AddSingleton<IStackConfig, FileStackConfig>();
        services.AddSingleton<IFileOperations, FileOperations>();
        services.AddSingleton<CliExecutionContext>();

        services.AddSingleton<IGitClient, GitClient>();
        services.AddSingleton<GitHubClient>();
        services.AddSingleton<IGitHubClient>(provider =>
        {
            var baseClient = provider.GetRequiredService<GitHubClient>();
            var safe = new SafeGitHubClient(baseClient, provider.GetRequiredService<ILogger<SafeGitHubClient>>());
            var cache = provider.GetRequiredService<IMemoryCache>();
            return new CachingGitHubClient(safe, cache);
        });

        services.AddSingleton<IStackActions, StackActions>();
        RegisterCommandHandlers(services);
        RegisterCommands(services);
    }

    private static void RegisterCommandHandlers(IServiceCollection services)
    {
        services.AddTransient<NewStackCommandHandler>();
        services.AddTransient<UpdateStackCommandHandler>();
        services.AddTransient<DeleteStackCommandHandler>();
        services.AddTransient<RenameStackCommandHandler>();
        services.AddTransient<ListStacksCommandHandler>();
        services.AddTransient<CleanupStackCommandHandler>();
        services.AddTransient<StackStatusCommandHandler>();
        services.AddTransient<StackSwitchCommandHandler>();

        services.AddTransient<AddBranchCommandHandler>();
        services.AddTransient<NewBranchCommandHandler>();
        services.AddTransient<RemoveBranchCommandHandler>();

        services.AddTransient<PullStackCommandHandler>();
        services.AddTransient<PushStackCommandHandler>();
        services.AddTransient<SyncStackCommandHandler>();

        services.AddTransient<CreatePullRequestsCommandHandler>();
        services.AddTransient<OpenPullRequestsCommandHandler>();
    }

    private static void RegisterCommands(IServiceCollection services)
    {
        services.AddSingleton<StackRootCommand>();

        services.AddTransient<NewStackCommand>();
        services.AddTransient<UpdateStackCommand>();
        services.AddTransient<DeleteStackCommand>();
        services.AddTransient<RenameStackCommand>();
        services.AddTransient<ListStacksCommand>();
        services.AddTransient<CleanupStackCommand>();
        services.AddTransient<StackStatusCommand>();
        services.AddTransient<StackSwitchCommand>();

        services.AddTransient<BranchCommand>();
        services.AddTransient<AddBranchCommand>();
        services.AddTransient<NewBranchCommand>();
        services.AddTransient<RemoveBranchCommand>();

        services.AddTransient<PullStackCommand>();
        services.AddTransient<PushStackCommand>();
        services.AddTransient<SyncStackCommand>();

        services.AddTransient<PullRequestsCommand>();
        services.AddTransient<CreatePullRequestsCommand>();
        services.AddTransient<OpenPullRequestsCommand>();

        services.AddTransient<ConfigCommand>();
        services.AddTransient<OpenConfigCommand>();
    }
}