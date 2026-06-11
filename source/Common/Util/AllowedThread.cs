using System;
using System.Collections.Generic;
using System.Threading;

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
/// WARNING: Please remember to revoke the thread after the process is completed; prefer
/// using (new AllowedThread()) so the allowance is released even when the action throws.
/// </remarks>
public class AllowedThread : IDisposable
{
    public static int CurrentThreadId => Thread.CurrentThread.ManagedThreadId;
    private static readonly Dictionary<int, int> _allowedThreadIds = new Dictionary<int, int>();

    public AllowedThread()
    {
        AllowThisThread();
    }

    public void Dispose()
    {
        RevokeThisThread();
    }

    /// <summary>
    /// Increments this thread's allowance count. Every call must be balanced by exactly one
    /// <see cref="RevokeThisThread"/>; the count is what lets scopes nest on one thread.
    /// </summary>
    public static void AllowThisThread()
    {
        lock (_allowedThreadIds)
        {
            _allowedThreadIds.TryGetValue(CurrentThreadId, out var count);
            _allowedThreadIds[CurrentThreadId] = count + 1;
        }
    }

    /// <summary>
    /// Decrements this thread's allowance count. The thread only stops being allowed once the
    /// outermost scope ends (count reaches zero); an unmatched revoke is a no-op.
    /// </summary>
    public static void RevokeThisThread()
    {
        lock (_allowedThreadIds)
        {
            if (!_allowedThreadIds.TryGetValue(CurrentThreadId, out var count)) return;

            if (count <= 1)
            {
                _allowedThreadIds.Remove(CurrentThreadId);
            }
            else
            {
                _allowedThreadIds[CurrentThreadId] = count - 1;
            }
        }
    }

    public static bool IsThisThreadAllowed()
    {
        lock (_allowedThreadIds)
        {
            return _allowedThreadIds.ContainsKey(CurrentThreadId);
        }
    }
}
