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
    private bool IsTwoClientNative => store.Current?.Mode == NavalLabMode.TwoClientNative;
    private bool UsesFactoryLifecycle => IsTwoClientNative || store.Current?.Mode == NavalLabMode.FactoryAuthorityProbe;

    private bool NativeAssignmentValid => IsTwoClientNative && failure == null && ready.Count == 2
        && hosts.TryGet(store.Current.InstanceId, out var host) && host.Epoch == 1
        && store.Current.Controllers.All(id => players.TryGetPeer(id, out _)
            && (host.HostControllerId == id || host.SuccessorControllerIds.Contains(id)));

    private void ReceiveStations(MessagePayload<NetworkNavalLabStations> payload)
    {
        GameThread.RunSafe(() =>
        {
            if (!IsTwoClientNative || payload.What.IncarnationId != store.Current.IncarnationId) return;
            try
            {
                if (ModInformation.IsClient)
                {
                    if (payload.What.Phase == "commit" && controller is INavalNativeController native)
                        native.ApplyStations(payload.What);
                    return;
                }
                if (!NativeAssignmentValid || payload.Who is not NetPeer peer || !players.TryGetPlayer(peer, out var player))
                    throw new InvalidOperationException("native.stations_without_authority");
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
                else throw new InvalidOperationException("native.invalid_station_phase");
            }
            catch (Exception exception)
            {
                if (ModInformation.IsServer) HoldFixture(exception.ToString());
                else network.SendAll(new NetworkNavalLabFault(store.Current.IncarnationId, exception.ToString()));
            }
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

    private void ReceiveNativeInput(MessagePayload<NetworkNavalLabHelmInput> payload)
    {
        bool readyAtReceive = ModInformation.IsServer ? nativeReleaseSent : (controller as INavalNativeController)?.NativeInputIngressReady == true;
        GameThread.RunSafe(() =>
        {
            if (!IsTwoClientNative || payload.What.IncarnationId != store.Current.IncarnationId || !readyAtReceive) return;
            if (ModInformation.IsClient)
            {
                (controller as INavalNativeController)?.ReceiveNativeInput(payload.What, readyAtReceive);
                return;
            }
            if (!NativeAssignmentValid || payload.Who is not NetPeer peer || !players.TryGetPlayer(peer, out var player)
                || !nativeState.AcceptInput(player.ControllerId, payload.What, DateTime.UtcNow.Ticks)) return;
            if (hosts.TryGet(store.Current.InstanceId, out var host) && players.TryGetPeer(host.HostControllerId, out var target))
                network.Send(target, payload.What);
        }, context: nameof(ReceiveNativeInput));
    }
}
#endif
