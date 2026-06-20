using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Actions.Handlers;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Caravans.Messages;
using GameInterface.Services.ObjectManager;
using Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Caravans.Handlers;

internal class FormPlayerClanCaravanHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<FormPlayerClanCaravanHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public FormPlayerClanCaravanHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<PlayerClanCaravanFormed>(Handle_PlayerClanCaravanFormed);
        messageBroker.Subscribe<FormPlayerClanCaravan>(Handle_FormPlayerClanCaravan);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerClanCaravanFormed>(Handle_PlayerClanCaravanFormed);
        messageBroker.Unsubscribe<FormPlayerClanCaravan>(Handle_FormPlayerClanCaravan);
    }

    private void Handle_PlayerClanCaravanFormed(MessagePayload<PlayerClanCaravanFormed> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.CaravanLeader, out var caravanLeaderId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.CurrentSettlement, out var currentSettlementId)) return;

        var message = new FormPlayerClanCaravan(mainHeroId, caravanLeaderId, currentSettlementId, obj.What.IsElite, obj.What.ShouldCreateConvoy, obj.What.GoldCost);
        network.SendAll(message);
    }

    private void Handle_FormPlayerClanCaravan(MessagePayload<FormPlayerClanCaravan> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var mainHero)) return;
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.CaravanLeaderId, out var caravanLeader)) return;
        if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.CurrentSettlementId, out var currentSettlement)) return;

        GameThread.Run(() =>
        {
            try
            {
                LeaveSettlementAction.ApplyForCharacterOnly(caravanLeader);
                PartyTemplateObject randomCaravanTemplate = CaravanHelper.GetRandomCaravanTemplate(currentSettlement.Culture, obj.What.IsElite, !obj.What.ShouldCreateConvoy);
                CaravanPartyComponent.CreateCaravanParty(mainHero, currentSettlement, randomCaravanTemplate, false, caravanLeader, null, obj.What.IsElite);
                GiveGoldAction.ApplyForCharacterToSettlement(mainHero, currentSettlement, obj.What.GoldCost, false);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(FormPlayerClanCaravan));
            }
        });
    }
}
