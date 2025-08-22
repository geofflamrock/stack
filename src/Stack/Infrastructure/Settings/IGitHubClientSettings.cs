namespace Stack.Infrastructure.Settings;

public interface IGitHubClientSettings
{
    bool Verbose { get; }
    string? WorkingDirectory { get; }
}

public interface IGitHubClientSettingsUpdater  
{
    void UpdateSettings(bool verbose, string? workingDirectory);
}