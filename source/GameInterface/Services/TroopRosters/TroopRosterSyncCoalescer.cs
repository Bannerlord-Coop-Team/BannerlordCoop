using Common;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters;

/// <summary>
/// Batches authoritative <see cref="TroopRoster"/> content changes so the server sends at most one
/// whole-roster snapshot per roster per frame. A single battle wounds or recruits many troops on the
/// same roster, and snapshotting each individual change would send a whole roster per change, so the
/// changes are collected here and flushed once per frame instead.
/// </summary>
/// <remarks>
/// One instance per co-op session (registered per lifetime scope), so the server and any in-process
/// clients each get their own; <see cref="TroopRosterSnapshotHandler"/> marks rosters dirty and installs
/// <see cref="Flush"/>, and the main loop drives <see cref="Update"/> once per frame.
/// </remarks>
public sealed class TroopRosterSyncCoalescer : IUpdateable
{
    private readonly HashSet<TroopRoster> dirtyRosters = new HashSet<TroopRoster>();
    private readonly object dirtyLock = new object();

    /// <summary>
    /// Invoked on the game thread once per frame with the rosters that changed since the previous
    /// flush. Set by the owning handler while a session is active, null otherwise (changes are still
    /// collected but not sent).
    /// </summary>
    public Action<IReadOnlyList<TroopRoster>> Flush { get; set; }

    public int Priority => UpdatePriority.MainLoop.TroopRosterSnapshot;

    /// <summary>
    /// Marks a roster as changed so its contents are snapshotted on the next flush. Idempotent within
    /// a frame: marking the same roster repeatedly still yields a single snapshot.
    /// </summary>
    public void MarkDirty(TroopRoster roster)
    {
        if (roster == null) return;

        lock (dirtyLock)
        {
            dirtyRosters.Add(roster);
        }
    }

    public void Update(TimeSpan frameTime)
    {
        // Capture once: Flush is installed/cleared by the owning handler, so reading it twice could
        // race. With no sink, leave the dirty set intact so changes are sent once a sink is installed
        // rather than silently dropped.
        var flush = Flush;
        if (flush == null) return;

        TroopRoster[] toFlush;
        lock (dirtyLock)
        {
            if (dirtyRosters.Count == 0) return;

            toFlush = new TroopRoster[dirtyRosters.Count];
            dirtyRosters.CopyTo(toFlush);
            dirtyRosters.Clear();
        }

        flush(toFlush);
    }
}
