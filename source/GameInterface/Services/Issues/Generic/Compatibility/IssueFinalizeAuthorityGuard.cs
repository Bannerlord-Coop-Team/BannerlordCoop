using System;

namespace GameInterface.Services.Issues.Generic;

public sealed class IssueFinalizeAuthorityGuard : IDisposable
{
    [ThreadStatic]
    private static int count;

    public IssueFinalizeAuthorityGuard() => count++;
    public void Dispose() => count = count > 0 ? count - 1 : 0;
    public static bool IsActive => count > 0;
}
