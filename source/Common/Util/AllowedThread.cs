using System;
using System.Collections.Generic;
using System.Threading;

namespace Common.Util;

/// <summary>
/// Class for registering specific threads as "allowed"
/// </summary>
/// <remarks>
/// WARNING: Please remember to revoke the thread after the process is completed.
/// </remarks>
public class AllowedThread : IDisposable
{
    public static int CurrentThreadId => Thread.CurrentThread.ManagedThreadId;
    private static readonly HashSet<int> _allowedThreadIds = new HashSet<int>();

    public AllowedThread()
    {
        AllowThisThread();
    }

    public void Dispose()
    {
        RevokeThisThread();
    }

    public static void AllowThisThread()
    {
        lock(_allowedThreadIds)
        {
            _allowedThreadIds.Add(CurrentThreadId);
        }
    }

    public static void RevokeThisThread()
    {
        lock (_allowedThreadIds)
        {
            _allowedThreadIds.Remove(CurrentThreadId);
        }
    }

    public static bool IsThisThreadAllowed() => _allowedThreadIds.Contains(CurrentThreadId);
}
