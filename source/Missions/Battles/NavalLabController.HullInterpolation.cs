#if DEBUG
using System;
using System.Linq;
using Missions.Messages;
using TaleWorlds.Library;

namespace Missions.Battles;

public sealed partial class NavalLabController
{
    private const float HullInterpolationSeconds = 0.05f;
    private sealed class HullStream
    {
        internal NetworkNavalLabShipSample Target;
        internal MatrixFrame Start, Written, Observed;
        internal bool HasWritten;
        internal float Elapsed, Alpha;
        internal long Accepted, Applied, AcceptedCallback, AppliedCallback, ApplicationOrdinal, OwnerWriteRejects;
        internal string LastReject;
    }
    private readonly HullStream[] hullStreams = { new HullStream(), new HullStream() };
    private readonly long[] shipSentSequences = new long[2];
    private INavalLabShipAdapter ShipAdapter => adapter as INavalLabShipAdapter;
    private bool CanWriteFollowerHull => NativeControlsReady && !disposed
        && Mission != null && Mission == TaleWorlds.MountAndBlade.Mission.Current;

    private void ClearHullTargets()
    {
        foreach (var stream in hullStreams) stream.Target = null;
    }

    private void SendOwnedShip()
    {
        int slot = Array.IndexOf(manifest.Controllers, session.OwnControllerId);
        var sample = ShipAdapter.CaptureOwnedShip(shipSentSequences[slot] + 1, callback);
        if (sample == null) return;
        shipSentSequences[slot] = sample.Sequence;
        relay.SendAll(sample);
    }

    public void ReceiveShipSample(NetworkNavalLabShipSample sample)
    {
        if (!IsTwoClientNative || sample.Slot < 0 || sample.Slot >= 2) return;
        var stream = hullStreams[sample.Slot];
        if (manifest.Controllers[sample.Slot] == session.OwnControllerId)
        {
            stream.OwnerWriteRejects++;
            stream.LastReject = "owner_incoming_write";
            return;
        }
        if (!CanWriteFollowerHull || sample.InstanceId != manifest.InstanceId || sample.IncarnationId != manifest.IncarnationId
            || sample.ShipId != manifest.Ships[sample.Slot] || sample.OriginalOwner != manifest.Controllers[sample.Slot]
            || sample.AuthorityRevision != 1 || sample.Sequence <= stream.Accepted || !sample.IsValid
            || sample.DeadlineUtcTicks <= DateTime.UtcNow.Ticks || sample.DeadlineUtcTicks > DateTime.UtcNow.AddSeconds(1).Ticks)
        { stream.LastReject = "identity_readiness_sequence_or_expiry"; return; }
        try
        {
            if (!ShipAdapter.ValidateForeignShip(sample)) { stream.LastReject = "presentation_inventory_or_lifecycle"; return; }
            ShipAdapter.AcceptForeignShip(sample);
            stream.Start = stream.HasWritten ? stream.Written : adapter.ReadFrames()[sample.Slot];
            stream.Target = sample;
            stream.Accepted = sample.Sequence;
            stream.AcceptedCallback = callback;
            stream.Elapsed = 0;
            stream.LastReject = null;
        }
        catch (Exception exception) { FailFactoryProbe(exception.ToString()); }
    }

    private void TickFollowerHull(float dt)
    {
        if (!IsTwoClientNative) return;
        if (!CanWriteFollowerHull) { ClearHullTargets(); return; }
        if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0) return;
        for (int slot = 0; slot < hullStreams.Length; slot++)
        {
            var stream = hullStreams[slot];
            var target = stream.Target;
            if (target == null) continue;
            if (target.DeadlineUtcTicks <= DateTime.UtcNow.Ticks)
            { stream.Target = null; stream.LastReject = "expired"; continue; }
            if (stream.Elapsed >= HullInterpolationSeconds) continue;
            stream.Elapsed = Math.Min(HullInterpolationSeconds, stream.Elapsed + Math.Min(dt, HullInterpolationSeconds));
            stream.Alpha = stream.Elapsed / HullInterpolationSeconds;
            var frame = MatrixFrame.Lerp(stream.Start, NetworkNavalLabOarPresentation.ToFrame(target.Frame), stream.Alpha);
            if (!NetworkNavalLabOarPresentation.ValidFrame(NetworkNavalLabOarPresentation.FromFrame(frame))
                || !ShipAdapter.ApplyForeignShipFrame(slot, frame))
                throw new InvalidOperationException("native.foreign_frame_apply_refused:" + slot);
            stream.Written = frame;
            stream.HasWritten = true;
            stream.Observed = adapter.ReadFrames()[slot];
            stream.ApplicationOrdinal++;
            stream.Applied = target.Sequence;
            stream.AppliedCallback = callback;
        }
    }

    private object[] ShipStreamStatus() => manifest.Ships.Select((id, slot) =>
    {
        var stream = hullStreams[slot];
        return (object)new
        {
            slot, shipId = id, originalOwner = manifest.Controllers[slot], revision = 1,
            localRole = manifest.Controllers[slot] == session.OwnControllerId ? "owner" : "foreign",
            sentSequence = shipSentSequences[slot], acceptedSequence = stream.Accepted, appliedSequence = stream.Applied,
            sourceCallback = stream.Target?.SourceCallback, stream.AcceptedCallback, stream.AppliedCallback,
            stream.ApplicationOrdinal, stream.Alpha, stream.OwnerWriteRejects, stream.LastReject,
            targetFrame = stream.Target?.Frame,
            writtenFrame = stream.HasWritten ? NetworkNavalLabOarPresentation.FromFrame(stream.Written) : null,
            observedFrame = stream.HasWritten ? NetworkNavalLabOarPresentation.FromFrame(stream.Observed) : null
        };
    }).ToArray();

    private object HullInterpolationStatus() => new { windowSeconds = HullInterpolationSeconds, isTeleportation = false, ships = ShipStreamStatus() };
}
#endif
