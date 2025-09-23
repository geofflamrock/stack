namespace Stack.Infrastructure.Settings;

public class CliExecutionContext
{
    public string WorkingDirectory { get; set; } = Environment.CurrentDirectory;
}
