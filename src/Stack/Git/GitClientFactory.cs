using Microsoft.Extensions.Logging;

namespace Stack.Git
{
    public interface IGitClientFactory
    {
        IGitClient Create(string path);
    }

    public class GitClientFactory(ILoggerFactory loggerFactory) : IGitClientFactory
    {
        public IGitClient Create(string path)
        {
            var gitLogger = loggerFactory.CreateLogger<GitClient>();
            return new GitClient(gitLogger, path);
        }
    }
}
