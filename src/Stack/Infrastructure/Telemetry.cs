using System.Diagnostics;

namespace Stack.Infrastructure;

public static class Telemetry
{
    public const string ActivitySourceName = "Stack.Cli";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
        => ActivitySource.StartActivity(name, kind);
}
