using Common.Messaging;
using GameInterface.Registry.Messages;

namespace GameInterface.Registry.Handlers;

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
        messageBroker.Subscribe<ClearAllRegistries>(Handle);
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

    private void Handle(MessagePayload<ClearAllRegistries> payload)
    {
        registryCollection.ClearRegistries();

        messageBroker.Publish(this, new AllRegistriesCleared());
    }
}
