using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;

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

        // Register Git clients with wrapper pattern for runtime settings
        services.AddSingleton<GitClientWrapper>();
        services.AddSingleton<IGitClient>(provider => provider.GetRequiredService<GitClientWrapper>());
        services.AddSingleton<IGitClientSettingsUpdater>(provider => provider.GetRequiredService<GitClientWrapper>());

        services.AddSingleton<GitHubClientWrapper>();
        services.AddSingleton<IGitHubClient>(provider => provider.GetRequiredService<GitHubClientWrapper>());
        services.AddSingleton<IGitHubClientSettingsUpdater>(provider => provider.GetRequiredService<GitHubClientWrapper>());

        // Register stack actions
        services.AddSingleton<IStackActions, StackActions>();
    }
}