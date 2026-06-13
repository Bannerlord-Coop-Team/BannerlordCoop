using Common.Logging;
using Common.Messaging;
using Common.Network;
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
        if (!objectManager.TryGetObjectWithLogging<MapEventParty>(payload.What.MapEventPartyId, out var mapEventParty))
            return;

        mapEventParty.Update();
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

        if (!objectManager.TryGetObjectWithLogging<MapEventParty>(obj.MapEventPartyId, out var mapEventParty))
            return;

        mapEventParty._roster = FlattenedTroopSerializer.Deserialize(obj.FlattenedTroops, objectManager);

        messageBroker.Publish(this, new MapEventTroopsUpdated(mapEventParty));
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

        if (!objectManager.TryGetObjectWithLogging(obj.MapEventPartyId, out MapEventParty mapEventParty))
            return;

        try
        {
            var troopDescriptor = new UniqueTroopDescriptor(obj.TroopSeed);
            var troop = mapEventParty._roster[troopDescriptor].Troop;

            mapEventParty.OnTroopKilled(troopDescriptor);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling NetworkTroopKilled message for MapEventParty with ID {MapEventPartyId}", obj.MapEventPartyId);
        }
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

        if (!objectManager.TryGetObjectWithLogging(obj.MapEventPartyId, out MapEventParty mapEventParty))
            return;

        try
        {
            var troopDescriptor = new UniqueTroopDescriptor(obj.TroopSeed);
            mapEventParty.OnTroopWounded(troopDescriptor);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling NetworkTroopWounded message for MapEventParty with ID {MapEventPartyId}", obj.MapEventPartyId);
        }
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

        if (!objectManager.TryGetObjectWithLogging(obj.MapEventPartyId, out MapEventParty mapEventParty))
            return;

        try
        {
            var troopDescriptor = new UniqueTroopDescriptor(obj.TroopSeed);
            mapEventParty.OnTroopRouted(troopDescriptor);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling NetworkTroopRouted message for MapEventParty with ID {MapEventPartyId}", obj.MapEventPartyId);
        }
    }
}
