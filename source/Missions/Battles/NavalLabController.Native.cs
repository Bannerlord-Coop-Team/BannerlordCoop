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
        hostAcceptedInputSequences = nativeInputSequences.ToArray(),
        hostInputRemainingSeconds = nativeInputDeadlines.Select(deadline => Math.Max(0, deadline - Now)).ToArray(),
        ready = NativeControlsReady
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
        NativeAdapter.ConfigureNative(() => NativeControlsReady, input =>
        {
            if (NativeControlsReady) relay.SendAll(input);
        });
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
