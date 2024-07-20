using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.BesiegerCamps.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;

namespace GameInterface.Services.BesiegerCamps.Handlers;
internal class BesiegerCampPropertyHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BesiegerCampLifetimeHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;


    public BesiegerCampPropertyHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<BesiegerCampSiegeEventChanged>(Handle);
        messageBroker.Subscribe<NetworkChangeBesiegerCampSiegeEvent>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BesiegerCampSiegeEventChanged>(Handle);
        messageBroker.Unsubscribe<NetworkChangeBesiegerCampSiegeEvent>(Handle);
    }


    private void Handle(MessagePayload<BesiegerCampSiegeEventChanged> payload)
    {
        if (objectManager.TryGetId(payload.What.BesiegerCamp, out var besiegerCampId) == false) return;
        if (objectManager.TryGetId(payload.What.SiegeEvent, out var siegeEventId) == false) return;

        network.SendAll(new NetworkChangeBesiegerCampSiegeEvent(besiegerCampId, siegeEventId));
    }


    private void Handle(MessagePayload<NetworkChangeBesiegerCampSiegeEvent> payload)
    {
        if (objectManager.TryGetObject<BesiegerCamp>(payload.What.BesiegerCampId, out var besiegerCamp) == false) return;
        if (objectManager.TryGetObject<SiegeEvent>(payload.What.SiegeEventId, out var siegeEvent) == false) return;

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                besiegerCamp.SiegeEvent = siegeEvent;
            }
        });
        
    }
}
