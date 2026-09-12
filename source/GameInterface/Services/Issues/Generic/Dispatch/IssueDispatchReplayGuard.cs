using System;

namespace GameInterface.Services.Issues.Generic.Dispatch;

public sealed class IssueDispatchReplayGuard : IDisposable
{
    [ThreadStatic]
    private static int _count;

    public IssueDispatchReplayGuard() => _count++;

    public void Dispose() => _count = _count > 0 ? _count - 1 : 0;

    public static bool IsActive => _count > 0;
}
