#if DEBUG
using System;
using System.Linq;
using System.Threading;
using Missions.Messages;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace Missions.Naval;

internal sealed partial class NavalLabBehavior
{
    private readonly bool[] originalOwnerHullRoles;
    // Per-hull counters, sized to the manifest's ships in the constructor.
    private readonly long[] shipFixedEntries;
    private readonly long[] shipParallelEntries;
    private readonly long[] shipActiveFixedEntries;
    private readonly long[] shipActiveParallelEntries;
    private readonly long[] shipForceEntries;
    private readonly long[] shipTargetRefreshes;
    private readonly long[] shipOwnerWriteRejects;
    private readonly long[] shipSentSequences;
    private readonly long[] shipAcceptedSequences;
    private readonly long[] shipAppliedSequences;
    private readonly NetworkNavalLabSailState[] foreignSailStates;
    private long factoryUnattributedForceEntries;
    private long nativeInputCallback;
    private long nativeInputApplyCallback;

    private bool OwnsFactoryHull(int slot) => slot >= 0 && slot < originalOwnerHullRoles.Length
        && originalOwnerHullRoles[slot];

    // This diagnostic changes body integration only, never original-owner input or snapshot authority.
    private bool FactoryBodyExpectedActive(int slot) => slot >= 0 && slot < originalOwnerHullRoles.Length
        && (manifest.AllPhysicsProbe || OwnsFactoryHull(slot));

    // Secondary hulls send frame and sail only; presentation, ropes and stations stay flagship-only.
    internal NetworkNavalLabShipSample CaptureOwnedShip(int slot, long sequence, long callback)
    {
        if (!PresentationReady || !OwnsFactoryHull(slot) || slot >= Ships.Length) return null;
        bool flagship = manifest.IsFlagship(slot);
        var presentation = flagship ? CapturePresentation(sequence)?[slot] : null;
        var sail = ReadSailState(Ships[slot], manifest.Ships[slot]);
        if ((flagship && presentation == null) || sail == null) return null;
        var sample = new NetworkNavalLabShipSample(manifest.InstanceId, manifest.IncarnationId, slot,
            manifest.Ships[slot], ownControllerId, sequence, callback,
            NetworkNavalLabOarPresentation.FromFrame(Ships[slot].GlobalFrame), presentation, sail, flagship ? CaptureRopes() : null);
        if (!sample.IsValid) throw new InvalidOperationException("native.invalid_owned_sample");
        shipSentSequences[slot] = sequence;
        return sample;
    }

    private NetworkNavalLabFrames PresentationFrames(NetworkNavalLabShipSample sample)
    {
        var values = new NetworkNavalLabPresentation[2];
        values[sample.Slot] = sample.Presentation;
        return new NetworkNavalLabFrames(sample.IncarnationId, sample.AuthorityRevision, sample.Sequence,
            null, sample.SourceCallback, sailDeadlineUtcTicks: sample.DeadlineUtcTicks, presentation: values);
    }

    internal bool ValidateForeignShip(NetworkNavalLabShipSample sample)
    {
        if (sample.Slot < 0 || sample.Slot >= Ships.Length) return false;
        if (OwnsFactoryHull(sample.Slot)) { shipOwnerWriteRejects[sample.Slot]++; return false; }
        if (!PresentationReady || sample.InstanceId != manifest.InstanceId || sample.IncarnationId != manifest.IncarnationId
            || sample.ShipId != manifest.Ships[sample.Slot] || sample.OriginalOwner != manifest.ShipController(sample.Slot)
            || !sample.IsValid || sample.Sequence <= shipAcceptedSequences[sample.Slot]) return false;
        if (!manifest.IsFlagship(sample.Slot)) return sample.Presentation == null && sample.Ropes == null;
        return sample.Presentation != null && ValidatePresentation(PresentationFrames(sample)) && ValidateRopes(sample.Slot, sample.Ropes);
    }

    internal void AcceptForeignShip(NetworkNavalLabShipSample sample)
    {
        if (!ValidateForeignShip(sample)) throw new InvalidOperationException("native.foreign_sample_refused");
        if (manifest.IsFlagship(sample.Slot))
        {
            AcceptRopes(sample.Slot, sample.Ropes);
            AcceptPresentation(PresentationFrames(sample));
        }
        foreignSailStates[sample.Slot] = sample.SailState;
        shipAcceptedSequences[sample.Slot] = sample.Sequence;
    }

    internal bool ApplyForeignShipFrame(int slot, MatrixFrame frame)
    {
        if (slot < 0 || slot >= Ships.Length) return false;
        if (OwnsFactoryHull(slot)) { shipOwnerWriteRejects[slot]++; return false; }
        if (!PresentationReady || !factoryMaterialized || !factoryReleased) return false;
        CheckFactoryAssignment();
        var entity = Ships[slot].GameEntity;
        if (!entity.IsValid || entity.HasDynamicRigidBodyAndActiveSimulation() != manifest.AllPhysicsProbe)
            throw new InvalidOperationException("native.foreign_body_active_or_invalid:" + slot);
        entity.SetGlobalFrame(frame, isTeleportation: false);
        entity.UpdateAttachedNavigationMeshFaces();
        RefreshFollowerStationTargets(slot);
        shipAppliedSequences[slot] = shipAcceptedSequences[slot];
        return true;
    }

    internal object InspectShipAuthority() => new
    {
        expectedBothBodiesActive = manifest.AllPhysicsProbe,
        simultaneousNativePhysicsAndNetworkWrites = manifest.AllPhysicsProbe,
        productionValid = false,
        unattributedFixedEntries = Interlocked.Read(ref factoryPreCompletionFixedEntries),
        unattributedForceEntries = Interlocked.Read(ref factoryUnattributedForceEntries),
        ships = manifest.Ships.Select((id, slot) => new
        {
            slot, shipId = id, originalOwner = manifest.ShipController(slot), revision = 1, flagship = manifest.IsFlagship(slot),
            localRole = OwnsFactoryHull(slot) ? "owner" : "foreign",
            expectedActiveBeforeTerminal = FactoryBodyExpectedActive(slot),
            nativeController = slot < Ships.Length ? Ships[slot]?.Controller?.ControllerType.ToString() ?? "None" : "unavailable",
            nativeAutoUpdateController = slot < Ships.Length && Ships[slot] != null ? (bool?)Ships[slot]._autoUpdateController : null,
            activeBody = slot < Ships.Length && Ships[slot]?.GameEntity.IsValid == true
                ? (bool?)Ships[slot].GameEntity.HasDynamicRigidBodyAndActiveSimulation() : null,
            fixedEntries = Interlocked.Read(ref shipFixedEntries[slot]), parallelEntries = Interlocked.Read(ref shipParallelEntries[slot]),
            activeFixedEntries = Interlocked.Read(ref shipActiveFixedEntries[slot]), activeParallelEntries = Interlocked.Read(ref shipActiveParallelEntries[slot]),
            forceAttempts = Interlocked.Read(ref shipForceEntries[slot]), targetRefreshes = shipTargetRefreshes[slot],
            sentSequence = shipSentSequences[slot], acceptedSequence = shipAcceptedSequences[slot], appliedSequence = shipAppliedSequences[slot],
            ownerIncomingWriteRejects = shipOwnerWriteRejects[slot],
            nativeInputApplyCallback = OwnsFactoryHull(slot) ? nativeInputApplyCallback : 0,
            presentationSource = OwnsFactoryHull(slot) ? "owned_native_completed" : "accepted_foreign",
            foreignSailState = foreignSailStates[slot], lastReject = Blocker
        }).ToArray()
    };
}
#endif
