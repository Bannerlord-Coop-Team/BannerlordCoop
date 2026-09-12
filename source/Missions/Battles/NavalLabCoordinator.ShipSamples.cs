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
    private readonly long[] shipSampleSequences = new long[2];
    private readonly string[] shipSampleRejections = new string[2];

    private void CheckStationPresentation(NetworkNavalLabStations value)
    {
        if (!value.HasPresentationInventory || value.Keys == null || value.Keys.Any(key => !value.OarKeys.Contains(key)))
            throw new InvalidOperationException("native.invalid_presentation_inventory");
        var prior = nativeState.Stations.FirstOrDefault(stations => stations.Ship == value.Ship);
        if (prior != null && (!prior.SailKeys.SequenceEqual(value.SailKeys) || !prior.OarKeys.SequenceEqual(value.OarKeys)
            || !prior.OarSides.SequenceEqual(value.OarSides)))
            throw new InvalidOperationException("native.presentation_inventory_changed");
    }

    private void ReceiveShipSample(MessagePayload<NetworkNavalLabShipSample> payload)
    {
        GameThread.RunSafe(() =>
        {
            var sample = payload.What;
            if (!IsTwoClientNative || sample.IncarnationId != store.Current.IncarnationId) return;
            if (ModInformation.IsClient)
            {
                // Campaign clients receive this typed stream only over their server connection.
                (controller as NavalLabController)?.ReceiveShipSample(sample);
                return;
            }
            if (sample.Slot < 0 || sample.Slot >= 2) return;
            int slot = sample.Slot;
            var manifest = store.Current;
            string reject = null;
            if (!NativeAssignmentValid || !nativeReleaseSent || !nativeState.Ready) reject = "not_ready_or_connected";
            else if (payload.Who is not NetPeer peer || !players.TryGetPlayer(peer, out var player)
                || player.ControllerId != manifest.Controllers[slot]) reject = "sender_not_original_owner";
            else if (sample.InstanceId != manifest.InstanceId || sample.ShipId != manifest.Ships[slot]
                || sample.OriginalOwner != manifest.Controllers[slot] || sample.AuthorityRevision != 1) reject = "identity_or_revision";
            else if (sample.Sequence <= shipSampleSequences[slot]) reject = "stale_sequence";
            else if (!sample.IsValid || sample.DeadlineUtcTicks <= DateTime.UtcNow.Ticks
                || sample.DeadlineUtcTicks > DateTime.UtcNow.AddSeconds(1).Ticks) reject = "invalid_or_expired";
            else
            {
                var inventory = nativeState.Stations.Single(stations => stations.Ship == slot);
                if (!sample.Presentation.Sails.Select(sail => sail.Key).SequenceEqual(inventory.SailKeys)
                    || !sample.Presentation.Oars.Select(oar => oar.Key).SequenceEqual(inventory.OarKeys)
                    || !sample.Presentation.Oars.Select(oar => oar.Side).SequenceEqual(inventory.OarSides)) reject = "unknown_inventory";
            }
            shipSampleRejections[slot] = reject;
            if (reject != null) return;
            shipSampleSequences[slot] = sample.Sequence;
            foreach (var owner in manifest.Controllers.Where(owner => owner != manifest.Controllers[slot]))
                if (players.TryGetPeer(owner, out var target)) network.Send(target, sample);
        }, context: nameof(ReceiveShipSample));
    }
}
#endif
