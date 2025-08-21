namespace Stack.Tests.Helpers;

public class TemporaryDirectory(string DirectoryPath) : IDisposable
{
    public string DirectoryPath { get; } = DirectoryPath;

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try
        {
            Directory.Delete(DirectoryPath, true);
        }
        catch (Exception)
        {
            // Ignore
        }
    }

    public static TemporaryDirectory Create()
    {
        var path = CreatePath();
        Directory.CreateDirectory(path);
        return new TemporaryDirectory(path);
    }

    public static string CreatePath()
    {
        return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    }
}
