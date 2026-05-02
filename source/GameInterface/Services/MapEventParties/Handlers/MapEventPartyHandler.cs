using Common.Messaging;
using Common.Network;
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
    }

    public void Dispose()
    {
        messageBroker.Subscribe<OnTroopKilledAttempted>(Handle_OnTroopKilledAttempted);
    }

    private void Handle_OnTroopKilledAttempted(MessagePayload<OnTroopKilledAttempted> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.MapEventParty, out var mapEventPartyId))
        {
            return;
        }

        var message = new NetworkTroopKilled(mapEventPartyId, obj.TroopSeed);

        network.SendAll(message);
    }

    private void Handle_NetworkTroopKilled(MessagePayload<NetworkTroopKilled> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetObjectWithLogging(obj.MapEventPartyId, out MapEventParty mapEventParty))
        {
            return;
        }

        UniqueTroopDescriptor descriptor = new UniqueTroopDescriptor(obj.TroopSeed);
        mapEventParty.OnTroopKilled(descriptor);
    }
}
