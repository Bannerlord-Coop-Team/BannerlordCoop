using System;

namespace GameInterface.Services.Issues.Generic.Dispatch;

// Narrower alternative to AllowedThread for internal Issues-dispatch replay calls (ReplayQuestAccepted /
// server-side alternative-solution replay): AllowedThread also tells every OTHER unrelated patch to skip its
// own network-publish logic, which is wrong for a genuine server-authoritative replay that should still
// register/replicate normally. This flag only tells GenericQuestTypeDispatchPatches' own Postfixes to skip
// re-triggering themselves.
public sealed class IssueDispatchReplayGuard : IDisposable
{
    [ThreadStatic]
    private static int _count;

    public IssueDispatchReplayGuard() => _count++;

    public void Dispose() => _count = _count > 0 ? _count - 1 : 0;

    public static bool IsActive => _count > 0;
}
