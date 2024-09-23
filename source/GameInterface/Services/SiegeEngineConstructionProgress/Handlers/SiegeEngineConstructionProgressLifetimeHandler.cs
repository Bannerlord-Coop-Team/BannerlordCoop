using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.SiegeEngineConstructionProgresss.Messages;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngineConstructionProgresss.Handlers;
internal class SiegeEngineConstructionProgressLifetimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeEngineConstructionProgressLifetimeHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;


    public SiegeEngineConstructionProgressLifetimeHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<SiegeEngineConstructionProgressCreated>(Handle);
        messageBroker.Subscribe<NetworkCreateSiegeEngineConstructionProgress>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SiegeEngineConstructionProgressCreated>(Handle);
        messageBroker.Unsubscribe<NetworkCreateSiegeEngineConstructionProgress>(Handle);
    }


    private void Handle(MessagePayload<SiegeEngineConstructionProgressCreated> payload)
    {
        objectManager.AddNewObject(payload.What.Instance, out var id);

        network.SendAll(new NetworkCreateSiegeEngineConstructionProgress(id));
    }


    private void Handle(MessagePayload<NetworkCreateSiegeEngineConstructionProgress> payload)
    {
        var newSiegeEngineConstructionProgress = ObjectHelper.SkipConstructor<SiegeEngineConstructionProgress>();

        // TODO change setting to constructor patch
        //AccessTools.Field(typeof(SiegeEngineConstructionProgress), nameof(SiegeEngineConstructionProgress._besiegerParties)).SetValue(newSiegeEngineConstructionProgress, new MBList<MobileParty>());

        objectManager.AddExisting(payload.What.Id, newSiegeEngineConstructionProgress);
    }
}
