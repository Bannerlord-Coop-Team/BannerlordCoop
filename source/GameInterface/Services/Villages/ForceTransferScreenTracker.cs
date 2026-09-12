using System;

namespace GameInterface.Services.Villages;

// Single-slot attribution from a force-transfer loot screen open to its Done commit.
// A static is used because the Done prefixes are static Harmony patches; loot screens are
// modal so at most one force screen is ever open, and the entry is single-shot.
internal static class ForceTransferScreenTracker
{
    private static readonly object sync = new object();
    private static readonly TimeSpan AttributionTimeout = TimeSpan.FromMinutes(5);
    private static WeakReference<object> leftRoster;
    private static string requestId;
    private static DateTime notedAtUtc;

    public static void NoteLootScreenOpened(string forceTransferId, object leftLootRoster)
    {
        if (forceTransferId == null || leftLootRoster == null) return;

        lock (sync)
        {
            requestId = forceTransferId;
            leftRoster = new WeakReference<object>(leftLootRoster);
            notedAtUtc = DateTime.UtcNow;
        }
    }

    public static bool TryClaimForceTransferId(object leftLootRoster, out string forceTransferId)
    {
        lock (sync)
        {
            forceTransferId = null;
            if (requestId == null || leftLootRoster == null) return false;
            if (DateTime.UtcNow - notedAtUtc > AttributionTimeout)
            {
                ClearLocked();
                return false;
            }
            if (leftRoster == null || !leftRoster.TryGetTarget(out var target))
            {
                // Screen was cancelled and the dummy roster collected: drop the stale slot.
                ClearLocked();
                return false;
            }
            if (!ReferenceEquals(target, leftLootRoster)) return false;

            forceTransferId = requestId;
            ClearLocked();
            return true;
        }
    }

    public static void Clear()
    {
        lock (sync)
        {
            ClearLocked();
        }
    }

    private static void ClearLocked()
    {
        requestId = null;
        leftRoster = null;
        notedAtUtc = default;
    }
}
