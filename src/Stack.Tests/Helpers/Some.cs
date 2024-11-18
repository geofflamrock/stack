using System;

namespace Stack.Tests.Helpers;

public static class Some
{
    public static string Name() => Guid.NewGuid().ToString("N")[..8];
    public static Uri HttpsUri() => new($"https://{Name()}.com");
}
