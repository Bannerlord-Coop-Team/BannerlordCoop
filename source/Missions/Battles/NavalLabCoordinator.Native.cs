#if DEBUG
using System;
using System.Linq;
using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using LiteNetLib;
using Missions.Messages;

namespace Missions.Battles;

public sealed partial class NavalLabCoordinator
{
    private readonly INavalLabNativeState nativeState;
    private bool stationsCommitted;
    private volatile bool nativeReleaseSent;
    private bool HasFixture => store.Current != null;

    private bool NativeAssignmentValid => HasFixture && failure == null && ready.Count == 2
        && hosts.TryGet(store.Current.InstanceId, out var host) && host.Epoch == 1
        && store.Current.Controllers.All(id => players.TryGetPeer(id, out _)
            && (host.HostControllerId == id || host.SuccessorControllerIds.Contains(id)));

    private void ReceiveStations(MessagePayload<NetworkNavalLabStations> payload)
    {
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() =>
        {
            if (!HasFixture || payload.What.IncarnationId != store.Current.IncarnationId) return;
            try
            {
                if (!NativeAssignmentValid || payload.Who is not NetPeer peer || !players.TryGetPlayer(peer, out var player))
                    throw new InvalidOperationException("native.stations_without_authority");
                CheckStationPresentation(payload.What);
                if (payload.What.Phase == "offer")
                {
                    nativeState.Offer(player.ControllerId, payload.What);
                    TryCommitStations();
                }
                else if (payload.What.Phase == "ack")
                {
                    nativeState.Acknowledge(player.ControllerId, payload.What);
                    if (nativeState.Ready && !nativeReleaseSent)
                    {
                        nativeReleaseSent = true;
                        var id = Guid.NewGuid();
                        store.BeginOperation(id, "native-controls-ready");
                        foreach (var owner in store.Current.Controllers)
                            if (players.TryGetPeer(owner, out var target)) network.Send(target,
                                new NetworkNavalLabAction(store.Current.IncarnationId, id, 1, "native-controls-ready", 0, 0, false));
                    }
                }
                else if (payload.What.Phase == "release")
                {
                    // The original owner already applied its own native dismount; only the other owner replays it.
                    nativeState.Release(player.ControllerId, payload.What);
                    foreach (var owner in store.Current.Controllers.Where(owner => owner != player.ControllerId))
                        if (players.TryGetPeer(owner, out var target)) network.Send(target, payload.What);
                }
                else throw new InvalidOperationException("native.invalid_station_phase");
            }
            catch (Exception exception) { HoldFixture(exception.ToString()); }
        }, context: nameof(ReceiveStations));
    }

    private void TryCommitStations()
    {
        if (stationsCommitted || nativeState.Stations.Length != 2) return;
        stationsCommitted = true;
        foreach (var stations in nativeState.Stations)
            foreach (var owner in store.Current.Controllers)
                if (players.TryGetPeer(owner, out var target)) network.Send(target, stations.WithPhase("commit"));
    }

}
#endif
