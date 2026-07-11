using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEventParties.Handlers;

internal class MapEventPartyHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventPartyHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public MapEventPartyHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<OnTroopKilledAttempted>(Handle_OnTroopKilledAttempted);
        messageBroker.Subscribe<NetworkTroopKilled>(Handle_NetworkTroopKilled);

        messageBroker.Subscribe<OnTroopWoundedAttempted>(Handle_OnTroopWoundedAttempted);
        messageBroker.Subscribe<NetworkTroopWounded>(Handle_NetworkTroopWounded);

        messageBroker.Subscribe<OnTroopRoutedAttempted>(Handle_OnTroopRoutedAttempted);
        messageBroker.Subscribe<NetworkTroopRouted>(Handle_NetworkTroopRouted);

        messageBroker.Subscribe<OnTroopScoreHitAttempted>(Handle_OnTroopScoreHitAttempted);
        messageBroker.Subscribe<NetworkTroopScoreHit>(Handle_NetworkTroopScoreHit);

        // Client
        messageBroker.Subscribe<RequestMapEventPartyUpdate>(Handle_RequestMapEventPartyUpdate);
        messageBroker.Subscribe<NetworkRequestMapEventPartyUpdate>(Handle_NetworkRequestMapEventPartyUpdate);

        // Server
        messageBroker.Subscribe<MapEventPartyUpdated>(Handle_MapEventPartyUpdated);
        messageBroker.Subscribe<NetworkUpdateMapEventParty>(Handle_NetworkUpdateMapEventParty);
    }



    public void Dispose()
    {
        messageBroker.Unsubscribe<OnTroopKilledAttempted>(Handle_OnTroopKilledAttempted);
        messageBroker.Unsubscribe<NetworkTroopKilled>(Handle_NetworkTroopKilled);

        messageBroker.Unsubscribe<OnTroopWoundedAttempted>(Handle_OnTroopWoundedAttempted);
        messageBroker.Unsubscribe<NetworkTroopWounded>(Handle_NetworkTroopWounded);

        messageBroker.Unsubscribe<OnTroopRoutedAttempted>(Handle_OnTroopRoutedAttempted);
        messageBroker.Unsubscribe<NetworkTroopRouted>(Handle_NetworkTroopRouted);

        messageBroker.Unsubscribe<OnTroopScoreHitAttempted>(Handle_OnTroopScoreHitAttempted);
        messageBroker.Unsubscribe<NetworkTroopScoreHit>(Handle_NetworkTroopScoreHit);
        messageBroker.Unsubscribe<RequestMapEventPartyUpdate>(Handle_RequestMapEventPartyUpdate);
        messageBroker.Unsubscribe<NetworkRequestMapEventPartyUpdate>(Handle_NetworkRequestMapEventPartyUpdate);
        messageBroker.Unsubscribe<MapEventPartyUpdated>(Handle_MapEventPartyUpdated);
        messageBroker.Unsubscribe<NetworkUpdateMapEventParty>(Handle_NetworkUpdateMapEventParty);
    }

    private void Handle_RequestMapEventPartyUpdate(MessagePayload<RequestMapEventPartyUpdate> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.MapEventParty, out var mapEventPartyId))
            return;

        network.SendAll(new NetworkRequestMapEventPartyUpdate(mapEventPartyId));
    }

    private void Handle_NetworkRequestMapEventPartyUpdate(MessagePayload<NetworkRequestMapEventPartyUpdate> payload)
    {
        var obj = payload.What;

        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<MapEventParty>(obj.MapEventPartyId, out var mapEventParty))
                    return;

                mapEventParty.Update();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(NetworkRequestMapEventPartyUpdate));
            }
        });
    }

    private void Handle_MapEventPartyUpdated(MessagePayload<MapEventPartyUpdated> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.MapEventParty, out var mapEventPartyId))
            return;

        var flattenedTroops = FlattenedTroopSerializer.Serialize(obj.Roster, objectManager);

        var message = new NetworkUpdateMapEventParty(mapEventPartyId, flattenedTroops);
        network.SendAll(message);
    }

    private void Handle_NetworkUpdateMapEventParty(MessagePayload<NetworkUpdateMapEventParty> payload)
    {
        var obj = payload.What;

        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<MapEventParty>(obj.MapEventPartyId, out var mapEventParty))
                    return;

                // Troop ids are themselves created through the same ordered game-thread queue.
                mapEventParty._roster = FlattenedTroopSerializer.Deserialize(obj.FlattenedTroops, objectManager);

                messageBroker.Publish(this, new MapEventTroopsUpdated(mapEventParty));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(NetworkUpdateMapEventParty));
            }
        });
    }

    private void Handle_OnTroopKilledAttempted(MessagePayload<OnTroopKilledAttempted> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.MapEventParty, out var mapEventPartyId))
            return;

        var message = new NetworkTroopKilled(mapEventPartyId, obj.TroopSeed);

        network.SendAll(message);
    }

    private void Handle_NetworkTroopKilled(MessagePayload<NetworkTroopKilled> payload)
    {
        var obj = payload.What;

        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging(obj.MapEventPartyId, out MapEventParty mapEventParty))
                    return;

                var troopDescriptor = new UniqueTroopDescriptor(obj.TroopSeed);

                if (ModInformation.IsServer)
                {
                    mapEventParty.OnTroopKilled(troopDescriptor);
                }
                else
                {
                    // Only the scoreboard tally; Party.MemberRoster arrives separately.
                    using (new AllowedThread())
                    {
                        mapEventParty.Troops.OnTroopKilled(troopDescriptor);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling NetworkTroopKilled message for MapEventParty with ID {MapEventPartyId}", obj.MapEventPartyId);
            }
        });
    }

    private void Handle_OnTroopWoundedAttempted(MessagePayload<OnTroopWoundedAttempted> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.MapEventParty, out var mapEventPartyId))
            return;

        var message = new NetworkTroopWounded(mapEventPartyId, obj.TroopSeed);

        network.SendAll(message);
    }

    private void Handle_NetworkTroopWounded(MessagePayload<NetworkTroopWounded> payload)
    {
        var obj = payload.What;

        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging(obj.MapEventPartyId, out MapEventParty mapEventParty))
                    return;

                var troopDescriptor = new UniqueTroopDescriptor(obj.TroopSeed);

                if (ModInformation.IsServer)
                {
                    mapEventParty.OnTroopWounded(troopDescriptor);
                }
                else
                {
                    // Only the scoreboard tally; Party.MemberRoster arrives separately.
                    using (new AllowedThread())
                    {
                        mapEventParty.Troops.OnTroopWounded(troopDescriptor);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling NetworkTroopWounded message for MapEventParty with ID {MapEventPartyId}", obj.MapEventPartyId);
            }
        });
    }

    private void Handle_OnTroopScoreHitAttempted(MessagePayload<OnTroopScoreHitAttempted> payload)
    {
        var obj = payload.What;

        // The host plays on the server process: its own live-battle hits (CoopAgentOrigin) are applied
        // directly — there is no other machine to report to.
        if (ModInformation.IsServer)
        {
            ApplyTroopScoreHit(obj.MapEventParty, obj.TroopSeed, obj.AttackedTroop, obj.Damage, obj.IsFatal, obj.IsSimulatedHit);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(obj.MapEventParty, out var mapEventPartyId))
            return;

        if (!objectManager.TryGetIdWithLogging(obj.AttackedTroop, out var attackedTroopId))
            return;

        network.SendAll(new NetworkTroopScoreHit(mapEventPartyId, obj.TroopSeed, attackedTroopId, obj.Damage, obj.IsFatal, obj.IsSimulatedHit));
    }

    private void Handle_NetworkTroopScoreHit(MessagePayload<NetworkTroopScoreHit> payload)
    {
        // Server-authoritative: clients receive the resulting contribution through the
        // MapEventParty._contributionToBattle autosync and the roster xp through the roster sync.
        if (ModInformation.IsClient) return;

        var obj = payload.What;

        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging(obj.MapEventPartyId, out MapEventParty mapEventParty))
                    return;

                if (!objectManager.TryGetObjectWithLogging(obj.AttackedTroopId, out CharacterObject attackedTroop))
                    return;

                ApplyTroopScoreHit(mapEventParty, obj.TroopSeed, attackedTroop, obj.Damage, obj.IsFatal, obj.IsSimulatedHit);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling NetworkTroopScoreHit message for MapEventParty with ID {MapEventPartyId}", obj.MapEventPartyId);
            }
        });
    }

    // [Server] Run the original scorer (the prefix lets the server through): roster xp, the hero combat-hit
    // event and ContributionToBattle, whose field store the autosync intercepts and broadcasts to every
    // client. The attacker's weapon is not carried over the wire; null is what the native simulation path
    // passes too, so the models tolerate it.
    private void ApplyTroopScoreHit(MapEventParty mapEventParty, int troopSeed, CharacterObject attackedTroop, int damage, bool isFatal, bool isSimulatedHit)
    {
        try
        {
            mapEventParty.OnTroopScoreHit(new UniqueTroopDescriptor(troopSeed), attackedTroop, damage, isFatal, isTeamKill: false, null, isSimulatedHit);
        }
        catch (KeyNotFoundException)
        {
            // The roster was re-flattened since the reporter captured the seed; a lost hit only shaves a
            // little xp/contribution, unlike a lost casualty, so drop it rather than guess a troop.
            Logger.Warning("Score hit for seed {TroopSeed} dropped: no matching troop in party {Party}'s roster", troopSeed, mapEventParty.Party?.Id);
        }
    }

    private void Handle_OnTroopRoutedAttempted(MessagePayload<OnTroopRoutedAttempted> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.MapEventParty, out var mapEventPartyId))
            return;

        var message = new NetworkTroopRouted(mapEventPartyId, obj.TroopSeed);

        network.SendAll(message);
    }

    private void Handle_NetworkTroopRouted(MessagePayload<NetworkTroopRouted> payload)
    {
        var obj = payload.What;

        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging(obj.MapEventPartyId, out MapEventParty mapEventParty))
                    return;

                var troopDescriptor = new UniqueTroopDescriptor(obj.TroopSeed);

                if (ModInformation.IsServer)
                {
                    mapEventParty.OnTroopRouted(troopDescriptor);
                }
                // Only the scoreboard tally (non-hero routs only, matching vanilla);
                // Party.MemberRoster arrives separately.
                else if (!mapEventParty.Troops[troopDescriptor].Troop.IsHero)
                {
                    using (new AllowedThread())
                    {
                        mapEventParty.Troops.OnTroopRouted(troopDescriptor);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling NetworkTroopRouted message for MapEventParty with ID {MapEventPartyId}", obj.MapEventPartyId);
            }
        });
    }
}
