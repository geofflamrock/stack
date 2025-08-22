using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure.Settings;

namespace Stack.Infrastructure;

public static class ServiceConfiguration
{
    public static IHost CreateHost()
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .UseConsoleLifetime();

        return builder.Build();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Register memory cache
        services.AddMemoryCache();

        // Register console components
        services.AddSingleton<IAnsiConsole>(provider =>
        {
            return AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.Detect,
                ColorSystem = ColorSystemSupport.Detect,
                Out = new AnsiConsoleOutput(Console.Error),
            });
        });

        services.AddSingleton<ILogger, ConsoleLogger>();
        services.AddSingleton<IInputProvider, ConsoleInputProvider>();

        // Register config
        services.AddSingleton<IStackConfig, FileStackConfig>();

        // Register file operations
        services.AddSingleton<IFileOperations, FileOperations>();

        // Register Git client settings
        services.AddSingleton<MutableGitClientSettings>();
        services.AddSingleton<IGitClientSettings>(provider => provider.GetRequiredService<MutableGitClientSettings>());
        services.AddSingleton<IGitClientSettingsUpdater>(provider => provider.GetRequiredService<MutableGitClientSettings>());
        services.AddSingleton<IGitClient, GitClient>();

        // Register GitHub client settings
        services.AddSingleton<MutableGitHubClientSettings>();
        services.AddSingleton<IGitHubClientSettings>(provider => provider.GetRequiredService<MutableGitHubClientSettings>());
        services.AddSingleton<IGitHubClientSettingsUpdater>(provider => provider.GetRequiredService<MutableGitHubClientSettings>());
        
        // Register GitHub client with caching
        services.AddSingleton<GitHubClient>();
        services.AddSingleton<IGitHubClient>(provider => 
        {
            var gitHubClient = provider.GetRequiredService<GitHubClient>();
            var cache = provider.GetRequiredService<IMemoryCache>();
            return new CachingGitHubClient(gitHubClient, cache);
        });

        // Register stack actions
        services.AddSingleton<IStackActions, StackActions>();
    }
}