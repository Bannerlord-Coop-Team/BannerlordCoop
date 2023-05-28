using Common.Messaging;
using GameInterface.Services.MobileParties;
using GameInterface.Services.Heroes.Messages;

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
        var payload = obj.What;

        heroRegistry.RegisterAllHeroes();
        partyRegistry.RegisterAllParties();

        messageBroker.Respond(obj.Who, new AllGameObjectsRegistered());
    }
}
