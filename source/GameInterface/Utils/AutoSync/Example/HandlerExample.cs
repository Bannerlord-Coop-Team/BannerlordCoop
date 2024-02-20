using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Utils.AutoSync.Example;
public class HandlerExample : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ILogger logger;

    public HandlerExample(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        ILogger logger)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.logger = logger;

        messageBroker.Subscribe<NewPlayerHeroRegistered>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NewPlayerHeroRegistered>(Handle);
    }

    private void Handle(MessagePayload<NewPlayerHeroRegistered> obj)
    {
        if (objectManager.TryGetObject(obj.What.Player.PartyStringId, out MobileParty party) == false)
        {
            logger.Error("Could not find {objType} with string id {stringId}", typeof(MobileParty), obj.What.Player.PartyStringId);
            return;
        }

        //partyInterface.ManageNewPlayerParty(party);
    }
}
