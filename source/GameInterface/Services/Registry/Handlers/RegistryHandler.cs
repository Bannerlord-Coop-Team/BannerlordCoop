using Common.Messaging;
using GameInterface.Services.MobileParties;
using GameInterface.Services.Heroes.Messages;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Registry.Handlers;

internal class RegistryHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IHeroRegistry heroRegistry;
    private readonly IMobilePartyRegistry partyRegistry;

    public RegistryHandler(
        IMessageBroker messageBroker,
        IHeroRegistry heroRegistry,
        IMobilePartyRegistry partyRegistry)
    {
        this.messageBroker = messageBroker;
        this.heroRegistry = heroRegistry;
        this.partyRegistry = partyRegistry;
        
        messageBroker.Subscribe<RegisterAllGameObjects>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RegisterAllGameObjects>(Handle);
    }

    private void Handle(MessagePayload<RegisterAllGameObjects> obj)
    {
        heroRegistry.RegisterAllHeroes();
        partyRegistry.RegisterAllParties();

        messageBroker.Publish(this, new AllGameObjectsRegistered());
    }
}
