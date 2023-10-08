using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Control;
using GameInterface.Services.MobileParties.Patches;
using System;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;

/// <summary>
/// Handles heroes of mobile party entities.
/// </summary>
public class MobilePartyBeHostileHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IControllerIdProvider controllerIdProvider;

    private string controllerId => controllerIdProvider.ControllerId;

    public MobilePartyBeHostileHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IControllerIdProvider controllerIdProvider) 
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.controllerIdProvider = controllerIdProvider;
        messageBroker.Subscribe<PartyBeHostile>(Handle);

    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyBeHostile>(Handle);
    }


    private void Handle(MessagePayload<PartyBeHostile> obj)
    {
        var payload = obj.What;

        PartyBase attackerParty = MobileParty.All.Find(x => x.StringId == payload.AttackerPartyId).Party;

        PartyBase defenderParty = MobileParty.All.Find(x => x.StringId == payload.DefenderPartyId).Party;

        BeHostileActionPatch.RunOriginalApplyInternal(attackerParty, defenderParty, payload.Value);
    }
}