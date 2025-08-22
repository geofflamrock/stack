namespace Stack.Infrastructure.Settings;

public class MutableGitHubClientSettings : IGitHubClientSettings, IGitHubClientSettingsUpdater
{
    public bool Verbose { get; private set; }
    public string? WorkingDirectory { get; private set; }

    public MutableGitHubClientSettings(bool verbose = false, string? workingDirectory = null)
    {
        Verbose = verbose;
        WorkingDirectory = workingDirectory;
    }

    public void UpdateSettings(bool verbose, string? workingDirectory)
    {
        Verbose = verbose;
        WorkingDirectory = workingDirectory;
    }
}