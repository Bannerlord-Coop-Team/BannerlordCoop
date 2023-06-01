using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MobileParties;
using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Handlers;

internal class SwitchHeroHandler : IHandler
{
    private readonly IHeroInterface heroInterface;
    private readonly IMessageBroker messageBroker;
    private readonly IMobilePartyRegistry partyRegistry;
    private readonly IObjectManager objectManager;
    private readonly IControlledEntityRegistry controlledEntityRegistry;

    public SwitchHeroHandler(IHeroInterface heroInterface, IControlledEntityRegistry controlledEntityRegistry, IMobilePartyRegistry partyRegistry, IObjectManager objectManager, IMessageBroker messageBroker)
    {
        this.heroInterface = heroInterface;
        this.controlledEntityRegistry = controlledEntityRegistry;
        this.partyRegistry = partyRegistry;
        this.objectManager = objectManager;
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<SwitchToHero>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SwitchToHero>(Handle);
    }

    private void Handle(MessagePayload<SwitchToHero> obj)
    {
        heroInterface.SwitchMainHero(obj.What.HeroId);

        if (!objectManager.TryGetObject(obj.What.HeroId, out Hero hero) ||
            !objectManager.TryGetId(hero.PartyBelongedTo, out string partyId))
        {
            return;
        }

        controlledEntityRegistry.RegisterAsControlled(controlledEntityRegistry.InstanceOwnerId, partyId);
    }
}
