using System;

namespace Stack.Tests.Helpers;

public static class Some
{
    static int next;
    public static int Int() => Interlocked.Increment(ref next);
    public static string Name() => Guid.NewGuid().ToString("N");
    public static string ShortName() => Guid.NewGuid().ToString("N").Substring(0, 8);
    public static string BranchName() => $"branch-{ShortName()}";
    public static Uri HttpsUri() => new($"https://{ShortName()}.com");
    public static string Email() => $"{ShortName()}@{ShortName()}.com";
}
