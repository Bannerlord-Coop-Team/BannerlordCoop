#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Missions.Messages;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

public interface INavalNativeController
{
    NetworkNavalLabStations CreateStations();
    void ReleaseNativeControls();
    object NativeControlStatus();
    bool NativeControlsReady { get; }
}

public sealed partial class NavalLabController : INavalNativeController
{
    private volatile bool nativeControlsReleased;
    private readonly Dictionary<int, NetworkNavalLabStations> pendingStations = new();
    private readonly HashSet<int> acknowledgedStations = new();
    private readonly long[] nativeInputSequences = new long[2];
    private readonly double[] nativeInputDeadlines = new double[2];
    private INavalNativeMissionAdapter NativeAdapter => adapter as INavalNativeMissionAdapter;
    public bool NativeControlsReady => nativeControlsReleased && released && hydrated
        && AssignmentValid && adapter.Blocker == null && NativeAgentAuthoritiesValid;
    // The registry walk runs once per tick; hot paths reuse it and keep the cheap lifecycle, epoch and blocker checks live.
    private volatile bool agentAuthoritiesValid;
    private bool FixtureAuthorityValid => agentAuthoritiesValid && released && hydrated && AssignmentValid && adapter.Blocker == null;
    private bool NativeReady => nativeControlsReleased && FixtureAuthorityValid;

    public object NativeControlStatus() => new
    {
        epoch = session.HostEpoch, localControllerCallback = callback,
        ownerSentFrameSequence = shipSentSequences[Array.IndexOf(manifest.Controllers, session.OwnControllerId)],
        hullInterpolation = HullInterpolationStatus(),
        ships = ShipStreamStatus(),
        nativeShips = (adapter as INavalLabShipAdapter)?.InspectShipAuthority(),
        ownerAppliedInputSequences = nativeInputSequences.ToArray(),
        ownerInputRemainingSeconds = nativeInputDeadlines.Select(deadline => Math.Max(0, deadline - Now)).ToArray(),
        ready = NativeControlsReady,
        stationMovement = coopMissionComponent.AgentMovementHandler.InspectNavalStationMovement()
    };

    private bool NativeAgentAuthoritiesValid => manifest.Combatants.All(id =>
        coopMissionComponent.AgentRegistry.TryGetAgentInfo(id, out var info)
        && info.OriginalOwner == manifest.Controllers[Array.IndexOf(manifest.Combatants, id) / NavalLabManifest.CrewPerShip]
        && info.CurrentAuthority == info.OriginalOwner && info.AuthorityRevision == 1
        && Array.IndexOf(adapter.Agents, info.Agent) == Array.IndexOf(manifest.Combatants, id));

    private void ConfigureNativeAdapter()
    {
        if (NativeAdapter == null) throw new InvalidOperationException("native.adapter_unavailable");
        if (adapter is not INavalHelmReplicationAdapter helm) throw new InvalidOperationException("native.helm_adapter_unavailable");
        bool deck = manifest.Mode == NavalLabMode.TwoClientNative && adapter is INavalDeckAdapter;
        coopMissionComponent.AgentMovementHandler.ConfigureNavalStationMovement(IsCommittedOarMovement, IsOccupiedHelmMovement,
            NativeHelmMovementRevision, AcceptNativeStationMovement, deck ? CaptureNavalDeck : null, deck ? ResolveNavalDeckFrame : null);
        helm.ConfigureHelmReplication(value =>
        {
            if (!NativeControlsReady)
                throw new InvalidOperationException("native.helm_send_without_authority");
            if (value.Phase == "offer" && manifest.Controllers[value.Ship] != session.OwnControllerId)
                throw new InvalidOperationException("native.helm_send_not_owner");
            relay.SendAll(value);
        }, agent => coopMissionComponent.AgentMovementHandler.Interpolator.Forget(agent));
        if (adapter is not INavalLabShipAdapter) throw new InvalidOperationException("native.ship_adapter_unavailable");
        NativeAdapter.ConfigureNative(() => NativeReady, input => ReceiveNativeInput(input, NativeControlsReady), release =>
        {
            if (!NativeControlsReady || release.Phase != "release"
                || manifest.Controllers[release.Ship] != session.OwnControllerId)
                throw new InvalidOperationException("native.station_release_without_authority");
            relay.SendAll(release);
        });
        (adapter as INavalRopeAdapter)?.ConfigureFinalRopes(final =>
        {
            // Sent from the owner's terminal hold, so readiness is already gone; only identity and lifetime gate it.
            if (disposed || final.IncarnationId != manifest.IncarnationId
                || manifest.Controllers[final.Slot] != session.OwnControllerId) return;
            relay.SendAll(final);
        });
    }

    // Accepted even after a hold: the owner's final rope state is how this client converges on the frozen lifecycle.
    private void ReceiveFinalRopes(NetworkNavalLabRopeFinal value)
    {
        if (manifest.Mode != NavalLabMode.TwoClientNative || !value.IsValid || value.ShipId != manifest.Ships[value.Slot]
            || manifest.Controllers[value.Slot] == session.OwnControllerId
            || Mission == null || Mission != TaleWorlds.MountAndBlade.Mission.Current) return;
        try { (adapter as INavalRopeAdapter)?.AcceptFinalRopes(value); }
        catch (Exception exception) { Fail(exception.ToString()); }
    }

    private long? NativeHelmMovementRevision(CoopAgentInfo info)
    {
        if (info == null) return null;
        int index = Array.IndexOf(manifest.Combatants, info.AgentId);
        if (index < 0 || index % NavalLabManifest.CrewPerShip != 0) return null;
        if (!NativeReady || Mission == null || Mission != TaleWorlds.MountAndBlade.Mission.Current
            || info.OriginalOwner != manifest.Controllers[index / NavalLabManifest.CrewPerShip]
            || info.CurrentAuthority != info.OriginalOwner || info.AuthorityRevision != 1
            || info.MovementScopeId != manifest.InstanceId + ":" + info.OriginalOwner || adapter.Agents[index] != info.Agent)
            return -1;
        return ((INavalHelmReplicationAdapter)adapter).HelmMovementRevision(info.AgentId, info.Agent);
    }

    // Called only for a released revision, which already bound the actor to this incarnation's captain identity.
    private bool CaptureNavalDeck(CoopAgentInfo info, Vec3 worldPosition, out Guid deckShip, out Vec3 deckLocal, out float deckSpeed)
    {
        deckShip = Guid.Empty;
        deckLocal = Vec3.Zero;
        deckSpeed = 0;
        int slot = Array.IndexOf(manifest.Controllers, session.OwnControllerId);
        if (slot < 0 || info?.OriginalOwner != session.OwnControllerId || !(NativeHelmMovementRevision(info) >= 1)
            || Array.IndexOf(manifest.Combatants, info.AgentId) != slot * NavalLabManifest.CrewPerShip
            || !((INavalDeckAdapter)adapter).TryCaptureOwnCaptainDeck(info.Agent, worldPosition, out int supportSlot,
                out deckLocal, out deckSpeed)
            || supportSlot < 0 || supportSlot >= manifest.Ships.Length)
            return false;
        deckShip = manifest.Ships[supportSlot];
        return true;
    }

    // A foreign captain keeps its origin identity and released revision; deckShip only selects the support hull.
    private bool ResolveNavalDeckFrame(Agent agent, Guid deckShip, out MatrixFrame hullFrame)
    {
        hullFrame = default;
        int index = Array.IndexOf(adapter.Agents, agent);
        int captainSlot = index / NavalLabManifest.CrewPerShip;
        int supportSlot = Array.IndexOf(manifest.Ships, deckShip);
        if (index < 0 || index % NavalLabManifest.CrewPerShip != 0 || captainSlot >= manifest.Controllers.Length
            || manifest.Controllers[captainSlot] == session.OwnControllerId || supportSlot < 0
            || !coopMissionComponent.AgentRegistry.TryGetAgentInfo(manifest.Combatants[index], out var info)
            || info.Agent != agent || !(NativeHelmMovementRevision(info) >= 1)) return false;
        return ((INavalDeckAdapter)adapter).TryGetCaptainDeckFrame(captainSlot, supportSlot, agent, out hullFrame);
    }

    private bool AcceptNativeStationMovement(CoopAgentInfo info, long revision)
    {
        // A committed foreign oar station holds its puppet; a late pre-commit pose must not pull it off the seat.
        if (info?.OriginalOwner != session.OwnControllerId && IsCommittedOarStation(info)) return false;
        var expected = NativeHelmMovementRevision(info);
        return !expected.HasValue || (expected.Value >= 0 && revision == expected.Value);
    }

    private void ReceiveHelmOccupancy(NetworkNavalLabHelmOccupancy value)
    {
        try
        {
            if (!NativeControlsReady || value.IncarnationId != manifest.IncarnationId
                || value.Epoch != session.HostEpoch || value.Epoch != 1 || value.Ship < 0 || value.Ship >= 2
                || value.ShipId != manifest.Ships[value.Ship]
                || value.CombatantId != manifest.Combatants[value.Ship * NavalLabManifest.CrewPerShip])
                throw new InvalidOperationException("native.helm_receive_without_identity_or_readiness");
            ((INavalHelmReplicationAdapter)adapter).ApplyHelmOccupancy(value);
        }
        catch (Exception exception) { Fail(exception.ToString()); }
    }

    private bool IsCommittedOarMovement(CoopAgentInfo info) =>
        info?.OriginalOwner == session.OwnControllerId && IsCommittedOarStation(info);

    private bool IsCommittedOarStation(CoopAgentInfo info)
    {
        if (!FixtureAuthorityValid || Mission == null || Mission != TaleWorlds.MountAndBlade.Mission.Current || info == null
            || info.CurrentAuthority != info.OriginalOwner || info.AuthorityRevision != 1) return false;
        int index = Array.IndexOf(manifest.Combatants, info.AgentId);
        if (index < 0 || index % NavalLabManifest.CrewPerShip == 0 || adapter.Agents[index] != info.Agent
            || !pendingStations.TryGetValue(index / NavalLabManifest.CrewPerShip, out var station)
            || station.Phase != "commit" || station.IncarnationId != manifest.IncarnationId || station.Epoch != 1)
            return false;
        return NativeAdapter.IsCommittedOarMovement(manifest.IncarnationId, info.AgentId, info.Agent);
    }

    private bool IsOccupiedHelmMovement(CoopAgentInfo info)
    {
        if (!NativeReady || Mission == null || Mission != TaleWorlds.MountAndBlade.Mission.Current
            || info == null || info.OriginalOwner != session.OwnControllerId || info.CurrentAuthority != info.OriginalOwner
            || info.AuthorityRevision != 1) return false;
        int slot = Array.IndexOf(manifest.Controllers, session.OwnControllerId);
        int index = slot * NavalLabManifest.CrewPerShip;
        if (slot < 0 || index >= adapter.Agents.Length || manifest.Combatants[index] != info.AgentId
            || adapter.Agents[index] != info.Agent || info.Agent != Mission.MainAgent || info.Agent != Mission.InitialPlayerAgent)
            return false;
        return NativeAdapter.IsOccupiedHelmMovement(manifest.IncarnationId, info.AgentId, info.Agent);
    }

    public NetworkNavalLabStations CreateStations()
    {
        if (!AssignmentValid || !hydrated || !NativeAgentAuthoritiesValid)
            throw new InvalidOperationException("native.station_authority_unavailable");
        return NativeAdapter.CreateStations();
    }

    private void ApplyStations(NetworkNavalLabStations stations)
    {
        if (stations.Phase == "release") { ApplyStationRelease(stations); return; }
        try
        {
            if (!AssignmentValid || !hydrated || !NativeAgentAuthoritiesValid
                || stations.IncarnationId != manifest.IncarnationId || stations.Epoch != session.HostEpoch)
                throw new InvalidOperationException("native.stations_stale");
            NativeAdapter.ApplyStations(stations);
            pendingStations[stations.Ship] = stations;
            // The committed seats now own the rower poses; drop any buffered pre-commit movement target.
            for (int i = 1; i < NavalLabManifest.CrewPerShip; i++)
                coopMissionComponent.AgentMovementHandler.Interpolator.Forget(adapter.Agents[(stations.Ship * NavalLabManifest.CrewPerShip) + i]);
        }
        catch (Exception exception) { Fail(exception.ToString()); }
    }

    // Replays the other owner's native rower dismount; the committed seat stops owning that rower's pose.
    private void ApplyStationRelease(NetworkNavalLabStations release)
    {
        if (terminal) return;
        try
        {
            if (!AssignmentValid || !hydrated || !NativeAgentAuthoritiesValid
                || release.IncarnationId != manifest.IncarnationId || release.Epoch != session.HostEpoch
                || release.Ship < 0 || release.Ship >= 2 || manifest.Controllers[release.Ship] == session.OwnControllerId
                || !pendingStations.TryGetValue(release.Ship, out var committed) || committed.Phase != "commit")
                throw new InvalidOperationException("native.station_release_stale_or_not_foreign");
            NativeAdapter.ApplyStations(release);
        }
        catch (Exception exception) { Fail(exception.ToString()); }
    }

    public void ReleaseNativeControls()
    {
        if (!AssignmentValid || acknowledgedStations.Count != 2 || !NativeAgentAuthoritiesValid)
        {
            Fail("native.early_controls_release");
            return;
        }
        nativeControlsReleased = true;
        agentAuthoritiesValid = true;
    }

    private void TickNativeControls()
    {
        if (!NativeAgentAuthoritiesValid) throw new InvalidOperationException("native.agent_authority_changed");
        agentAuthoritiesValid = true;
        foreach (var entry in pendingStations)
        {
            if (!NativeAdapter.ObserveStations(entry.Value)) throw new InvalidOperationException("native.station_occupancy_lost");
            if (acknowledgedStations.Add(entry.Key)) relay.SendAll(entry.Value.WithPhase("ack"));
        }
        for (int i = 0; i < nativeInputDeadlines.Length; i++)
            if (nativeInputDeadlines[i] > 0 && (!NativeControlsReady || Now >= nativeInputDeadlines[i]))
            {
                nativeInputDeadlines[i] = 0;
                NativeAdapter.NeutralizeNativeInput(i);
            }
    }

    private void ReceiveNativeInput(NetworkNavalLabHelmInput input, bool readyAtReceive)
    {
        if (!readyAtReceive || !NativeControlsReady || input.IncarnationId != manifest.IncarnationId
            || input.Epoch != session.HostEpoch || input.Ship < 0 || input.Ship >= 2
            || manifest.Controllers[input.Ship] != session.OwnControllerId || !input.IsValid
            || input.Sequence <= nativeInputSequences[input.Ship] || input.DeadlineUtcTicks <= DateTime.UtcNow.Ticks
            || input.DeadlineUtcTicks > DateTime.UtcNow.AddSeconds(1).Ticks) return;
        try
        {
            nativeInputSequences[input.Ship] = input.Sequence;
            nativeInputDeadlines[input.Ship] = Now + Math.Min(1, TimeSpan.FromTicks(input.DeadlineUtcTicks - DateTime.UtcNow.Ticks).TotalSeconds);
            if (input.HasHelm) NativeAdapter.ApplyNativeInput(input);
            else NativeAdapter.NeutralizeNativeInput(input.Ship);
        }
        catch (Exception exception) { Fail(exception.ToString()); }
    }
}
#endif
