using Common.Messaging;
using Common.Network.Coalescing;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.MobileParties.Messages;

/// <summary>
/// Merges volunteer snapshots by hero so every hero contributes only its latest array for the tick.
/// </summary>
internal sealed class VolunteerSnapshotPayload : ICoalescedPayload
{
    private readonly Dictionary<uint, uint[]> snapshots;

    public VolunteerSnapshotPayload(IReadOnlyDictionary<uint, uint[]> snapshots)
    {
        if (snapshots == null) throw new ArgumentNullException(nameof(snapshots));

        this.snapshots = Clone(snapshots);
    }

    public ICoalescedPayload Merge(ICoalescedPayload incoming)
    {
        if (incoming is not VolunteerSnapshotPayload other)
        {
            throw new ArgumentException(
                $"Cannot merge {incoming?.GetType().Name ?? "null"} into {nameof(VolunteerSnapshotPayload)}; " +
                "a coalesce key must use a single payload type.",
                nameof(incoming));
        }

        // SendCoalescer calls Merge under its lock, so update in place instead of copying all prior heroes.
        foreach (var pair in other.snapshots)
        {
            snapshots[pair.Key] = pair.Value;
        }

        return this;
    }

    public IMessage ToMessage() => new UpdateVolunteers(Clone(snapshots));

    private static Dictionary<uint, uint[]> Clone(IReadOnlyDictionary<uint, uint[]> source)
    {
        var clone = new Dictionary<uint, uint[]>(source.Count);
        foreach (var pair in source)
        {
            clone[pair.Key] = Clone(pair.Value);
        }
        return clone;
    }

    private static uint[] Clone(uint[] source)
    {
        if (source == null) return Array.Empty<uint>();

        var clone = new uint[source.Length];
        Array.Copy(source, clone, source.Length);
        return clone;
    }
}
