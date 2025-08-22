namespace Stack.Infrastructure.Settings;

public interface IGitClientSettings
{
    bool Verbose { get; }
    string? WorkingDirectory { get; }
}

public interface IGitClientSettingsUpdater
{
    void UpdateSettings(bool verbose, string? workingDirectory);
}