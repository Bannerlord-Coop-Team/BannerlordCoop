#if DEBUG
using System;
using System.Threading;

namespace Coop.Core.Common.Commands;

/// <summary>
/// Bridges pre-session commands to the process-owned co-op session lifecycle.
/// </summary>
public static class ProcessLifetimeClientSessionStarter
{
    private static Func<bool> startClientSession;

    public static void Configure(Func<bool> starter)
    {
        if (starter == null) throw new ArgumentNullException(nameof(starter));

        Volatile.Write(ref startClientSession, starter);
    }

    public static bool Start()
    {
        Func<bool> starter = Volatile.Read(ref startClientSession);
        if (starter == null)
        {
            throw new InvalidOperationException("The process client-session starter is unavailable.");
        }

        return starter();
    }

    internal static void Reset()
    {
        Volatile.Write(ref startClientSession, null);
    }
}
#endif
