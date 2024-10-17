using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using Common.Util;
using GameInterface.Services.PartyComponents.Data;
using GameInterface.Services.PartyComponents.Messages;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using Common.Logging;
using Serilog;
using Common;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyComponents.Handlers;
internal class MilitiaPartyComponentHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyComponentHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public MilitiaPartyComponentHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<MilitiaPartyComponentSettlementChanged>(Handle);
        messageBroker.Subscribe<NetworkChangeSettlementMilitiaPartyComponent>(Handle);
    }

    private void Handle(MessagePayload<MilitiaPartyComponentSettlementChanged> payload)
    {
        var component = payload.What.Instance;
        if (objectManager.TryGetId(component, out var componentID) == false)
        {
            Logger.Error("Changing settlement failed on server. {name} was not registered with PartyComponentRegistry\n"
                + "Callstack: {callstack}", typeof(MilitiaPartyComponent), Environment.StackTrace);

            return;
        }

        var message = new NetworkChangeSettlementMilitiaPartyComponent(componentID, payload.What.SettlementId);
        network.SendAll(message);
        return;
    }
    private void Handle(MessagePayload<NetworkChangeSettlementMilitiaPartyComponent> payload)
    {
        var componentId = payload.What.ComponentId;

        if (objectManager.TryGetObject(componentId, out MilitiaPartyComponent component) == false)
        {
            Logger.Error("{name} was not registered with PartyComponentRegistry\n"
                + "Callstack: {callstack}", typeof(MilitiaPartyComponent), Environment.StackTrace);

            return;
        }

        if (objectManager.TryGetObject(payload.What.SettlementId, out Settlement settlement) == false)
        {
            Logger.Error("Changing settlement failed on client. {name} was not registered with ObjectManager\n"
                + "Callstack: {callstack}", typeof(Settlement), Environment.StackTrace);

            return;
        }

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                component.Settlement = settlement;
            }
        }); 
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MilitiaPartyComponentSettlementChanged>(Handle);
        messageBroker.Unsubscribe<NetworkChangeSettlementMilitiaPartyComponent>(Handle);
    }
}


    

