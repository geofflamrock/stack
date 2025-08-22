namespace Stack.Infrastructure.Settings;

public class MutableGitClientSettings : IGitClientSettings, IGitClientSettingsUpdater
{
    public bool Verbose { get; private set; }
    public string? WorkingDirectory { get; private set; }

    public MutableGitClientSettings(bool verbose = false, string? workingDirectory = null)
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