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
    private readonly IControlledEntityRegistery controlledEntityRegistery;

    public SwitchHeroHandler(IHeroInterface heroInterface, IControlledEntityRegistery controlledEntityRegistery, IMobilePartyRegistry partyRegistry, IObjectManager objectManager, IMessageBroker messageBroker)
    {
        this.heroInterface = heroInterface;
        this.controlledEntityRegistery = controlledEntityRegistery;
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

        // TODO sus - this way of assigning owner id does not make any logical sense.
        controlledEntityRegistery.InstanceOwnerId = Guid.NewGuid();

        if (!objectManager.TryGetObject(obj.What.HeroId, out Hero hero) ||
            !objectManager.TryGetId(hero.PartyBelongedTo, out string partyId))
        {
            return;
        }

        controlledEntityRegistery.RegisterAsControlled(controlledEntityRegistery.InstanceOwnerId, partyId);
    }
}
