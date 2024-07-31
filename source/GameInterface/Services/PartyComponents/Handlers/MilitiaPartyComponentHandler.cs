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

        messageBroker.Subscribe<MilitiaPartyComponentSettlementFinalized>(Handle);
    }

    private void Handle(MessagePayload<MilitiaPartyComponentSettlementFinalized> payload)
    {
        var component = payload.What.Instance;
        if (objectManager.TryGetId(component, out var componentID) == false)
        {
            Logger.Error("MilitiaPartyComponent was not registered with PartyComponentRegistry");
            return;
        }

        var message = new NetworkFinalizeMilitiaPartyComponent(componentID);
        network.SendAll(message);
        return;
    }
    private void Handle(MessagePayload<NetworkFinalizeMilitiaPartyComponent> payload)
    {
        var componentId = payload.What.ComponentId;

        if (objectManager.TryGetObject(componentId, out MilitiaPartyComponent component) == false)
        {
            Logger.Error("MilitiaPartyComponent was not registered with PartyComponentRegistry");
            return;
        }

      /*  GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                component.OnFinalize();
            }
        }); */
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MilitiaPartyComponentSettlementFinalized>(Handle);
        messageBroker.Unsubscribe<NetworkFinalizeMilitiaPartyComponent>(Handle);
    }
}


    

