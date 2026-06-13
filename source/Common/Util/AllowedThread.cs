using Common.Logging;
using Serilog;
using System;

namespace Common.Util;

/// <summary>
/// Class for registering specific threads as "allowed"
/// </summary>
/// <remarks>
/// Allowances are reference counted per thread: each <see cref="AllowThisThread"/> (or
/// constructed scope) must be balanced by exactly one <see cref="RevokeThisThread"/> (or
/// dispose), and the thread stays allowed until the outermost scope ends. This matters when
/// scopes nest on one thread — e.g. a replicated action whose handler chain opens its own
/// scope while the outer action is still running. With a plain set instead of a count, the
/// inner dispose would revoke the outer scope too, re-enabling patch interception for the
/// remainder of the outer action.
/// The count lives in a [ThreadStatic] field, so reading or changing it never locks or
/// contends with other threads; it is read on every patched call.
/// WARNING: Please remember to revoke the thread after the process is completed; prefer
/// using (new AllowedThread()) so the allowance is released even when the action throws.
/// </remarks>
public class AllowedThread : IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<AllowedThread>();

    [ThreadStatic]
    private static int _allowedCount;

    public AllowedThread(bool stringId = false)
    {
        AllowThisThread(stringId);
    }

    public void Dispose()
    {
        RevokeThisThread();
    }

    /// <summary>
    /// Increments this thread's allowance count. Every call must be balanced by exactly one
    /// <see cref="RevokeThisThread"/>; the count is what lets scopes nest on one thread.
    /// </summary>
    public static void AllowThisThread(bool stringId = false)
    {
        // Log warning if server is using allowed thread (and not explicitly setting a string id)
        // Server calling allowed thread will not broadcast side effects to all clients
        if (ModInformation.IsServer && !stringId)
        {
            Logger.Warning("Server is using allowed thread. This likely should not be happening. {CallStack}", Environment.StackTrace);
        }

        _allowedCount++;
    }

    /// <summary>
    /// Decrements this thread's allowance count. The thread only stops being allowed once the
    /// outermost scope ends (count reaches zero); an unmatched revoke is a no-op.
    /// </summary>
    public static void RevokeThisThread()
    {
        if (_allowedCount > 0)
        {
            _allowedCount--;
        }
    }

    public static bool IsThisThreadAllowed() => _allowedCount > 0;
}
