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
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new TemporaryDirectory(path);
    }
}
