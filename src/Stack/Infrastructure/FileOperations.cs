namespace Stack.Infrastructure;

public interface IFileOperations
{
    void Copy(string sourceFileName, string destFileName, bool overwrite);
    bool Exists(string path);
    string GetTempPath();
}

public class FileOperations : IFileOperations
{
    public void Copy(string sourceFileName, string destFileName, bool overwrite)
    {
        File.Copy(sourceFileName, destFileName, overwrite);
    }

    public bool Exists(string path)
    {
        return File.Exists(path);
    }

    public string GetTempPath()
    {
        return Path.GetTempPath();
    }
}