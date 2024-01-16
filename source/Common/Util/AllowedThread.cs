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
    private static int CurrentThreadId => Thread.CurrentThread.ManagedThreadId;
    private static readonly HashSet<int> AllowedThreadIds = new HashSet<int>();

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
        lock(AllowedThreadIds)
        {
            AllowedThreadIds.Add(CurrentThreadId);
        }
    }

    public static void RevokeThisThread()
    {
        lock (AllowedThreadIds)
        {
            AllowedThreadIds.Remove(CurrentThreadId);
        }
    }

    public static bool IsThisThreadAllowed() => AllowedThreadIds.Contains(CurrentThreadId);
}
