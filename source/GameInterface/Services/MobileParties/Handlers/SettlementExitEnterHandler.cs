using Common.Extensions;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Handlers;

/// <summary>
/// Handles changes to parties for settlement entry and exit.
/// </summary>
internal class SettlementExitEnterHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SettlementExitEnterHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IMobilePartyInterface partyInterface;

    public SettlementExitEnterHandler(
        IMessageBroker messageBroker,
        IMobilePartyInterface partyInterface)
    {
        this.messageBroker = messageBroker;
        this.partyInterface = partyInterface;
        messageBroker.Subscribe<PartyEnterSettlement>(Handle);
        messageBroker.Subscribe<PartyLeaveSettlement>(Handle);
        messageBroker.Subscribe<StartSettlementEncounter>(Handle);
        messageBroker.Subscribe<EndSettlementEncounter>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyEnterSettlement>(Handle);
        messageBroker.Unsubscribe<PartyLeaveSettlement>(Handle);
        messageBroker.Unsubscribe<StartSettlementEncounter>(Handle);
        messageBroker.Unsubscribe<EndSettlementEncounter>(Handle);
    }

    private void Handle(MessagePayload<PartyEnterSettlement> obj)
    {
        var payload = obj.What;

        partyInterface.EnterSettlement(payload.PartyId, payload.SettlementId);
    }


    

    private void Handle(MessagePayload<PartyLeaveSettlement> obj)
    {
        var payload = obj.What;

        partyInterface.LeaveSettlement(payload.PartyId);
    }

    private void Handle(MessagePayload<StartSettlementEncounter> obj)
    {
        var payload = obj.What;

        partyInterface.StartPlayerSettlementEncounter(payload.PartyId, payload.SettlementId);
    }

    private void Handle(MessagePayload<EndSettlementEncounter> obj)
    {
        partyInterface.EndPlayerSettlementEncounter();
    }
}