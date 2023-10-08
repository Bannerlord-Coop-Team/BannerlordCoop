using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Entity.Data;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Control;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using SandBox.GauntletUI.Map;
using SandBox.View.Map;
using SandBox.ViewModelCollection.Nameplate;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;

/// <summary>
/// Handles heroes in mobile parties.
/// </summary>
internal class MobilePartyHeroesHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IMobilePartyInterface partyInterface;
    private readonly IControlledEntityRegistry controlledEntityRegistry;
    private readonly IObjectManager objectManager;
    private readonly IControllerIdProvider controllerIdProvider;
    private bool controlPartiesByDefault = false;

    private string ownerId => controllerIdProvider.ControllerId;
    public void Dispose()
    {
    }

    public MobilePartyHeroesHandler(
        IMessageBroker messageBroker, 
        IMobilePartyInterface partyInterface, 
        IControlledEntityRegistry controlledEntityRegistry,
        IObjectManager objectManager,
        IControllerIdProvider controllerIdProvider)
    {
        this.messageBroker = messageBroker;
        this.partyInterface = partyInterface;
        this.controlledEntityRegistry = controlledEntityRegistry;
        this.objectManager = objectManager;
        this.controllerIdProvider = controllerIdProvider;

        messageBroker.Subscribe<HeroAddedToParty>(Handle);
    }

    private void Handle(MessagePayload<HeroAddedToParty> obj)
    {
        var payload = obj.What;

        Hero hero = Hero.FindFirst(x => x.StringId == payload.HeroId);

        MobileParty party = MobileParty.All.Find(x => x.StringId == payload.PartyId);

        AddHeroToPartyPatch.RunOriginalApplyInternal(hero, party, payload.ShowNotification);
    }
}