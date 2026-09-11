#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using LiteNetLib;
using Missions.Messages;

namespace Missions.Battles;

public sealed partial class NavalLabCoordinator
{
    private readonly NetworkNavalLabHelmOccupancy[] helmOccupancy = new NetworkNavalLabHelmOccupancy[2];
    private readonly HashSet<string>[] helmAcknowledgements = { new(), new() };
    private bool HelmReplicasReady => helmOccupancy.All(state => state != null)
        && helmAcknowledgements.All(owners => owners.Count == 2);

    private void ReceiveHelmOccupancy(MessagePayload<NetworkNavalLabHelmOccupancy> payload)
    {
        GameThread.RunSafe(() =>
        {
            var value = payload.What;
            if (!IsTwoClientNative || value.IncarnationId != store.Current.IncarnationId) return;
            try
            {
                if (ModInformation.IsClient)
                {
                    if (controller is not INavalNativeController native)
                        throw new InvalidOperationException("native.helm_controller_missing");
                    native.ReceiveHelmOccupancy(value);
                    return;
                }
                var manifest = store.Current;
                if (!NativeAssignmentValid || !nativeReleaseSent || !nativeState.Ready
                    || payload.Who is not NetPeer peer || !players.TryGetPlayer(peer, out var player)
                    || !manifest.Controllers.Contains(player.ControllerId) || value.Epoch != 1
                    || value.Ship < 0 || value.Ship >= 2 || value.ShipId != manifest.Ships[value.Ship]
                    || value.CombatantId != manifest.Combatants[value.Ship * NavalLabManifest.CrewPerShip]
                    || value.Revision <= 0 || string.IsNullOrWhiteSpace(value.Key) || value.Key.Length > 256)
                    throw new InvalidOperationException("native.helm_invalid_identity_or_readiness");
                var prior = helmOccupancy[value.Ship];
                if (value.Phase == "offer")
                {
                    if (manifest.Controllers[value.Ship] != player.ControllerId)
                        throw new InvalidOperationException("native.helm_not_original_owner");
                    if (prior != null && value.Revision < prior.Revision) return;
                    if (prior != null && value.Revision == prior.Revision)
                    {
                        if (!value.SameState(prior)) throw new InvalidOperationException("native.helm_conflicting_revision");
                        return;
                    }
                    if (value.Revision != (prior?.Revision ?? 0) + 1 || value.Occupied == (prior?.Occupied ?? false)
                        || (prior != null && (prior.Key != value.Key || helmAcknowledgements[value.Ship].Count != 2)))
                        throw new InvalidOperationException("native.helm_out_of_order");
                    helmOccupancy[value.Ship] = value;
                    helmAcknowledgements[value.Ship].Clear();
                    BroadcastHelmOccupancy(value.WithPhase("commit"));
                }
                else if (value.Phase == "ack")
                {
                    if (prior != null && value.Revision < prior.Revision) return;
                    if (!value.SameState(prior)) throw new InvalidOperationException("native.helm_ack_mismatch");
                    if (helmAcknowledgements[value.Ship].Add(player.ControllerId)
                        && helmAcknowledgements[value.Ship].Count == 2)
                        BroadcastHelmOccupancy(value.WithPhase("confirmed"));
                }
                else throw new InvalidOperationException("native.helm_invalid_phase");
            }
            catch (Exception exception)
            {
                if (ModInformation.IsServer) HoldFixture(exception.ToString());
                else network.SendAll(new NetworkNavalLabFault(store.Current.IncarnationId, exception.ToString()));
            }
        }, context: nameof(ReceiveHelmOccupancy));
    }

    private void BroadcastHelmOccupancy(NetworkNavalLabHelmOccupancy value)
    {
        foreach (var owner in store.Current.Controllers)
            if (players.TryGetPeer(owner, out var target)) network.Send(target, value);
    }
}
#endif
