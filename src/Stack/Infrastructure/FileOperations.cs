namespace Stack.Infrastructure;

public interface IFileOperations
{
    void Create(string path);
    void Copy(string sourceFileName, string destFileName, bool overwrite);
    bool Exists(string path);
    string GetTempPath();
    void InsertText(string path, int index, string contents);
}

public class FileOperations : IFileOperations
{
    public void Create(string path)
    {
        File.Create(path).Close();
    }

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

    public void InsertText(string path, int index, string contents)
    {
        var lines = File.ReadAllLines(path).ToList();
        lines.Insert(index, contents);
        File.WriteAllLines(path, lines);
    }
}