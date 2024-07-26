using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEventSides.Handlers;
internal class MapEventSideDataHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;


    public MapEventSideDataHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<MapEventSideMobilePartyChanged>(Handle);
        messageBroker.Subscribe<NetworkMapEventSideChangeMobileParty>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MapEventSideMobilePartyChanged>(Handle);
        messageBroker.Unsubscribe<NetworkMapEventSideChangeMobileParty>(Handle);
    }

    private void Handle(MessagePayload<MapEventSideMobilePartyChanged> payload)
    {
        var payloadData = payload.What;

        if (objectManager.TryGetId(payloadData.MapEventSide, out var mapEventSideId) == false) return;
        if (objectManager.TryGetId(payloadData.MobileParty, out var mobilePartyId) == false) return;

        var message = new NetworkMapEventSideChangeMobileParty(mapEventSideId, mobilePartyId);

        network.SendAll(message);
    }

    private void Handle(MessagePayload<NetworkMapEventSideChangeMobileParty> payload)
    {
        var payloadData = payload.What;

        if (objectManager.TryGetObject<MapEventSide>(payloadData.MapEventSideId, out var mapEventSide) == false) return;
        if (objectManager.TryGetObject<MobileParty>(payloadData.MobilePartyId, out var mobileParty) == false) return;

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                mapEventSide.LeaderParty = mobileParty.Party;
            }
        });
    }
}
