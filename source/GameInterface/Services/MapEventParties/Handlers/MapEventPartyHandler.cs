using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEventParties.Handlers;

internal class MapEventPartyHandler : IHandler
{
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
    }

    public void Dispose()
    {
        messageBroker.Subscribe<OnTroopKilledAttempted>(Handle_OnTroopKilledAttempted);
    }

    private void Handle_OnTroopKilledAttempted(MessagePayload<OnTroopKilledAttempted> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.MapEventParty, out var mapEventPartyId))
            return;
        if (!objectManager.TryGetIdWithLogging(obj.Troop, out var troopId))
            return;

        var message = new NetworkTroopKilled(mapEventPartyId, troopId);

        network.SendAll(message);
    }

    private void Handle_NetworkTroopKilled(MessagePayload<NetworkTroopKilled> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetObjectWithLogging(obj.MapEventPartyId, out MapEventParty mapEventParty))
            return;
        if (!objectManager.TryGetObjectWithLogging(obj.TroopId, out CharacterObject troop))
            return;

        if (!troop.IsHero && mapEventParty.Party.IsActive)
        {
            mapEventParty.Party.MemberRoster.RemoveTroop(troop);
        }

        mapEventParty.DiedInBattle.AddToCounts(troop, 1);
    }

    private void Handle_OnTroopWoundedAttempted(MessagePayload<OnTroopWoundedAttempted> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.MapEventParty, out var mapEventPartyId))
            return;
        if (!objectManager.TryGetIdWithLogging(obj.Troop, out var troopId))
            return;

        var message = new NetworkTroopWounded(mapEventPartyId, troopId);

        network.SendAll(message);
    }

    private void Handle_NetworkTroopWounded(MessagePayload<NetworkTroopWounded> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetObjectWithLogging(obj.MapEventPartyId, out MapEventParty mapEventParty))
            return;
        if (!objectManager.TryGetObjectWithLogging(obj.TroopId, out CharacterObject troop))
            return;

        mapEventParty.Party.MemberRoster.WoundTroop(troop, 1);
        mapEventParty.WoundedInBattle.AddToCounts(troop, 1);
    }
}
