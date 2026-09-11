#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Missions.Messages;

namespace Missions.Battles;

public interface INavalNativeController
{
    NetworkNavalLabStations CreateStations();
    void ApplyStations(NetworkNavalLabStations stations);
    void ReleaseNativeControls();
    void ReceiveNativeInput(NetworkNavalLabHelmInput input, bool readyAtReceive);
    void ReceiveHelmOccupancy(NetworkNavalLabHelmOccupancy value);
    object NativeControlStatus();
    bool NativeControlsReady { get; }
    bool NativeInputIngressReady { get; }
}

public sealed partial class NavalLabController : INavalNativeController
{
    private bool IsTwoClientNative => manifest?.Mode == NavalLabMode.TwoClientNative;
    private volatile bool nativeControlsReleased;
    private readonly Dictionary<int, NetworkNavalLabStations> pendingStations = new();
    private readonly HashSet<int> acknowledgedStations = new();
    private long lastAppliedFrameSourceCallback;
    private long lastAppliedFrameUtcTicks;
    private readonly long[] nativeInputSequences = new long[2];
    private readonly double[] nativeInputDeadlines = new double[2];
    private INavalNativeMissionAdapter NativeAdapter => adapter as INavalNativeMissionAdapter;
    // Poll-thread admission reads only the published gate; registry validation stays on the game thread.
    public bool NativeInputIngressReady => nativeControlsReleased && released && factoryHydrated && !factoryTerminal;
    public bool NativeControlsReady => IsTwoClientNative && nativeControlsReleased && released && factoryHydrated
        && FactoryAssignmentValid && adapter.Blocker == null && NativeAgentAuthoritiesValid;

    public object NativeControlStatus() => new
    {
        epoch = session.HostEpoch, localControllerCallback = callback,
        lastReceivedFrameSequence = lastReceived, lastAppliedFrameSequence = lastApplied,
        hostSentFrameSequence = session.IsLocalHost ? (long?)sequence : null,
        lastAppliedSourceCallback = lastApplied > 0 ? (long?)lastAppliedFrameSourceCallback : null,
        lastAppliedUtcTicks = lastApplied > 0 ? (long?)lastAppliedFrameUtcTicks : null,
        hullInterpolation = HullInterpolationStatus(),
        hostAcceptedInputSequences = nativeInputSequences.ToArray(),
        hostInputRemainingSeconds = nativeInputDeadlines.Select(deadline => Math.Max(0, deadline - Now)).ToArray(),
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
        if (!IsTwoClientNative) return;
        if (NativeAdapter == null) throw new InvalidOperationException("native.adapter_unavailable");
        if (adapter is not INavalHelmReplicationAdapter helm) throw new InvalidOperationException("native.helm_adapter_unavailable");
        coopMissionComponent.AgentMovementHandler.ConfigureNavalStationMovement(IsCommittedOarMovement, IsOccupiedHelmMovement,
            NativeHelmMovementRevision, AcceptNativeHelmMovement);
        helm.ConfigureHelmReplication(value =>
        {
            if (!NativeControlsReady || !NativeAgentAuthoritiesValid)
                throw new InvalidOperationException("native.helm_send_without_authority");
            if (value.Phase == "offer" && manifest.Controllers[value.Ship] != session.OwnControllerId)
                throw new InvalidOperationException("native.helm_send_not_owner");
            relay.SendAll(value);
        }, agent => coopMissionComponent.AgentMovementHandler.Interpolator.Forget(agent));
        NativeAdapter.ConfigureNative(() => NativeControlsReady, input =>
        {
            if (NativeControlsReady) relay.SendAll(input);
        });
    }

    private long? NativeHelmMovementRevision(CoopAgentInfo info)
    {
        if (!IsTwoClientNative || info == null) return null;
        int index = Array.IndexOf(manifest.Combatants, info.AgentId);
        if (index < 0 || index % NavalLabManifest.CrewPerShip != 0) return null;
        if (!NativeControlsReady || Mission == null || Mission != TaleWorlds.MountAndBlade.Mission.Current
            || info.OriginalOwner != manifest.Controllers[index / NavalLabManifest.CrewPerShip]
            || info.CurrentAuthority != info.OriginalOwner || info.AuthorityRevision != 1
            || info.MovementScopeId != manifest.InstanceId + ":" + info.OriginalOwner || adapter.Agents[index] != info.Agent)
            return -1;
        return ((INavalHelmReplicationAdapter)adapter).HelmMovementRevision(info.AgentId, info.Agent);
    }

    private bool AcceptNativeHelmMovement(CoopAgentInfo info, long revision)
    {
        var expected = NativeHelmMovementRevision(info);
        return !expected.HasValue || (expected.Value >= 0 && revision == expected.Value);
    }

    public void ReceiveHelmOccupancy(NetworkNavalLabHelmOccupancy value)
    {
        try
        {
            if (!NativeControlsReady || !NativeAgentAuthoritiesValid || value.IncarnationId != manifest.IncarnationId
                || value.Epoch != session.HostEpoch || value.Epoch != 1 || value.Ship < 0 || value.Ship >= 2
                || value.ShipId != manifest.Ships[value.Ship]
                || value.CombatantId != manifest.Combatants[value.Ship * NavalLabManifest.CrewPerShip])
                throw new InvalidOperationException("native.helm_receive_without_identity_or_readiness");
            ((INavalHelmReplicationAdapter)adapter).ApplyHelmOccupancy(value);
        }
        catch (Exception exception) { FailFactoryProbe(exception.ToString()); }
    }

    private bool IsCommittedOarMovement(CoopAgentInfo info)
    {
        if (!IsTwoClientNative || !released || !factoryHydrated || !FactoryAssignmentValid
            || adapter.Blocker != null || Mission == null || Mission != TaleWorlds.MountAndBlade.Mission.Current
            || !NativeAgentAuthoritiesValid || info == null || info.OriginalOwner != session.OwnControllerId
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
        if (!IsTwoClientNative || !NativeControlsReady || Mission == null || Mission != TaleWorlds.MountAndBlade.Mission.Current
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
        if (!IsTwoClientNative || !FactoryAssignmentValid || !factoryHydrated || !NativeAgentAuthoritiesValid)
            throw new InvalidOperationException("native.station_authority_unavailable");
        return NativeAdapter.CreateStations();
    }

    public void ApplyStations(NetworkNavalLabStations stations)
    {
        try
        {
            if (!IsTwoClientNative || !FactoryAssignmentValid || !factoryHydrated || !NativeAgentAuthoritiesValid
                || stations.IncarnationId != manifest.IncarnationId || stations.Epoch != session.HostEpoch)
                throw new InvalidOperationException("native.stations_stale");
            NativeAdapter.ApplyStations(stations);
            pendingStations[stations.Ship] = stations;
        }
        catch (Exception exception) { FailFactoryProbe(exception.ToString()); }
    }

    public void ReleaseNativeControls()
    {
        if (!IsTwoClientNative || !FactoryAssignmentValid || acknowledgedStations.Count != 2 || !NativeAgentAuthoritiesValid)
        {
            FailFactoryProbe("native.early_controls_release");
            return;
        }
        nativeControlsReleased = true;
    }

    private void TickNativeControls()
    {
        if (!IsTwoClientNative) return;
        if (!NativeAgentAuthoritiesValid) throw new InvalidOperationException("native.agent_authority_changed");
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

    public void ReceiveNativeInput(NetworkNavalLabHelmInput input, bool readyAtReceive)
    {
        if (!readyAtReceive || !NativeControlsReady || !session.IsLocalHost || input.IncarnationId != manifest.IncarnationId
            || input.Epoch != session.HostEpoch || input.Ship < 0 || input.Ship >= 2 || !input.IsValid
            || input.Sequence <= nativeInputSequences[input.Ship] || input.DeadlineUtcTicks <= DateTime.UtcNow.Ticks
            || input.DeadlineUtcTicks > DateTime.UtcNow.AddSeconds(1).Ticks) return;
        try
        {
            nativeInputSequences[input.Ship] = input.Sequence;
            nativeInputDeadlines[input.Ship] = Now + Math.Min(1, TimeSpan.FromTicks(input.DeadlineUtcTicks - DateTime.UtcNow.Ticks).TotalSeconds);
            if (input.HasHelm) NativeAdapter.ApplyNativeInput(input);
            else NativeAdapter.NeutralizeNativeInput(input.Ship);
        }
        catch (Exception exception) { FailFactoryProbe(exception.ToString()); }
    }
}
#endif
