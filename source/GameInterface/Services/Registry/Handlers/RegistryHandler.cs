using Common.Messaging;
using GameInterface.Services.MobileParties;
using GameInterface.Services.Heroes.Messages;
using TaleWorlds.CampaignSystem;
using GameInterface.Services.Clans;
using GameInterface.Services.Settlements;
using GameInterface.Services.Armies;

namespace GameInterface.Services.Registry.Handlers;

internal class RegistryHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IRegistryCollection registryCollection;

    public RegistryHandler(
        IMessageBroker messageBroker,
        IRegistryCollection registryCollection)
    {
        this.messageBroker = messageBroker;
        this.registryCollection = registryCollection;
        messageBroker.Subscribe<RegisterAllGameObjects>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RegisterAllGameObjects>(Handle);
    }

    private void Handle(MessagePayload<RegisterAllGameObjects> obj)
    {
        foreach (var registry in registryCollection)
        {
            registry.RegisterAll();
        }

        messageBroker.Publish(this, new AllGameObjectsRegistered());
    }
}
