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
    private long[] shipSampleSequences = Array.Empty<long>();
    private string[] shipSampleRejections = Array.Empty<string>();

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
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() =>
        {
            var sample = payload.What;
            if (!HasFixture || sample.IncarnationId != store.Current.IncarnationId) return;
            var manifest = store.Current;
            if (sample.Slot < 0 || sample.Slot >= manifest.Ships.Length || sample.Slot >= shipSampleSequences.Length) return;
            int slot = sample.Slot;
            string owner = manifest.ShipController(slot);
            string reject = null;
            if (!NativeAssignmentValid || !nativeReleaseSent || !nativeState.Ready) reject = "not_ready_or_connected";
            else if (payload.Who is not NetPeer peer || !players.TryGetPlayer(peer, out var player)
                || player.ControllerId != owner) reject = "sender_not_original_owner";
            else if (sample.InstanceId != manifest.InstanceId || sample.ShipId != manifest.Ships[slot]
                || sample.OriginalOwner != owner || sample.AuthorityRevision != 1) reject = "identity_or_revision";
            else if (sample.Sequence <= shipSampleSequences[slot]) reject = "stale_sequence";
            else if (!sample.IsValid || sample.DeadlineUtcTicks <= DateTime.UtcNow.Ticks
                || sample.DeadlineUtcTicks > DateTime.UtcNow.AddSeconds(1).Ticks) reject = "invalid_or_expired";
            else if (!manifest.IsFlagship(slot))
            {
                // Secondary hulls carry no station inventory, so their samples are frame and sail only.
                if (sample.Presentation != null || sample.Ropes != null) reject = "secondary_presentation_or_ropes";
            }
            else
            {
                var inventory = nativeState.Stations.Single(stations => stations.Ship == slot);
                if (sample.Presentation == null || !sample.Presentation.Sails.Select(sail => sail.Key).SequenceEqual(inventory.SailKeys)
                    || !sample.Presentation.Oars.Select(oar => oar.Key).SequenceEqual(inventory.OarKeys)
                    || !sample.Presentation.Oars.Select(oar => oar.Side).SequenceEqual(inventory.OarSides)) reject = "unknown_inventory";
            }
            shipSampleRejections[slot] = reject;
            if (reject != null) return;
            shipSampleSequences[slot] = sample.Sequence;
            foreach (var foreign in manifest.Controllers.Where(id => id != owner))
                if (players.TryGetPeer(foreign, out var target)) network.Send(target, sample);
        }, context: nameof(ReceiveShipSample));
    }
}
#endif
