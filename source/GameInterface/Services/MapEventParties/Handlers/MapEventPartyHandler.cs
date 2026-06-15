using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
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

        GameLoopRunner.RunOnMainThread(() =>
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

        // Deserialize is pure CPU work, so it stays on the network thread; only the
        // roster assignment is deferred. The game loop reads and iterates the roster,
        // so writing it from the network thread races the tick.
        var roster = FlattenedTroopSerializer.Deserialize(obj.FlattenedTroops, objectManager);

        GameLoopRunner.RunOnMainThread(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<MapEventParty>(obj.MapEventPartyId, out var mapEventParty))
                    return;

                mapEventParty._roster = roster;

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

        // OnTroopKilled mutates the roster the game loop reads, so it is deferred to the
        // main thread. OnTroopKilled is Harmony-patched, so AllowedThread silences the
        // client prefix to stop it re-running and rebroadcasting in a loop.
        GameLoopRunner.RunOnMainThread(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging(obj.MapEventPartyId, out MapEventParty mapEventParty))
                    return;

                var troopDescriptor = new UniqueTroopDescriptor(obj.TroopSeed);
                var troop = mapEventParty._roster[troopDescriptor].Troop;

                using (new AllowedThread())
                {
                    mapEventParty.OnTroopKilled(troopDescriptor);
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

        // OnTroopWounded mutates the roster the game loop reads, so it is deferred to the
        // main thread. OnTroopWounded is Harmony-patched, so AllowedThread silences the
        // client prefix to stop it re-running and rebroadcasting in a loop.
        GameLoopRunner.RunOnMainThread(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging(obj.MapEventPartyId, out MapEventParty mapEventParty))
                    return;

                var troopDescriptor = new UniqueTroopDescriptor(obj.TroopSeed);

                using (new AllowedThread())
                {
                    mapEventParty.OnTroopWounded(troopDescriptor);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling NetworkTroopWounded message for MapEventParty with ID {MapEventPartyId}", obj.MapEventPartyId);
            }
        });
    }

    private void Handle_OnTroopRoutedAttempted(MessagePayload<OnTroopRoutedAttempted> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.MapEventParty, out var mapEventPartyId))
            return;

        var message = new NetworkTroopWounded(mapEventPartyId, obj.TroopSeed);

        network.SendAll(message);
    }

    private void Handle_NetworkTroopRouted(MessagePayload<NetworkTroopRouted> payload)
    {
        var obj = payload.What;

        // OnTroopRouted mutates the roster the game loop reads, so it is deferred to the
        // main thread. OnTroopRouted is Harmony-patched, so AllowedThread silences the
        // client prefix to stop it re-running and rebroadcasting in a loop.
        GameLoopRunner.RunOnMainThread(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging(obj.MapEventPartyId, out MapEventParty mapEventParty))
                    return;

                var troopDescriptor = new UniqueTroopDescriptor(obj.TroopSeed);

                mapEventParty.OnTroopRouted(troopDescriptor);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling NetworkTroopRouted message for MapEventParty with ID {MapEventPartyId}", obj.MapEventPartyId);
            }
        });
    }
}
