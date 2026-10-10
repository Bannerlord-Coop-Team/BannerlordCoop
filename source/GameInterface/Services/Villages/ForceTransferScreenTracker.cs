using System;

namespace GameInterface.Services.Villages;

// Single-slot attribution from a force-transfer loot screen open to its Done commit.
// A static is used because the Done prefixes are static Harmony patches; loot screens are
// modal so at most one force screen is ever open, and the entry is single-shot.
// The slot lives until claimed, overwritten, or cleared. Memory stays bounded
// because it is a single slot holding a weak reference to the roster.
internal static class ForceTransferScreenTracker
{
    private static readonly object sync = new object();
    private static WeakReference<object> leftRoster;
    private static string requestId;

    public static void NoteLootScreenOpened(string forceTransferId, object leftLootRoster)
    {
        if (forceTransferId == null || leftLootRoster == null) return;

        lock (sync)
        {
            requestId = forceTransferId;
            leftRoster = new WeakReference<object>(leftLootRoster);
        }
    }

    public static bool TryClaimForceTransferId(object leftLootRoster, out string forceTransferId)
    {
        lock (sync)
        {
            forceTransferId = null;
            if (requestId == null || leftLootRoster == null) return false;
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

    // Non-consuming check used to gate screen operations (e.g. troop upgrades)
    // that the force-transfer commit validation cannot honor.
    public static bool HasOpenForceTransferScreen()
    {
        lock (sync)
        {
            if (requestId == null) return false;
            if (leftRoster == null || !leftRoster.TryGetTarget(out _))
            {
                ClearLocked();
                return false;
            }

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
    }
}
