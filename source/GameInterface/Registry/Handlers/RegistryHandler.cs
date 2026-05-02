using Common.Messaging;
using GameInterface.AutoSync;
using GameInterface.Registry.Messages;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;

namespace GameInterface.Registry.Handlers;

internal class RegistryHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IRegistryCollection registryCollection;
    private readonly IAutoSyncPatchCollector autoSyncPatchCollector;
    private readonly IObjectManager objectManager;

    public RegistryHandler(
        IMessageBroker messageBroker,
        IRegistryCollection registryCollection,
        IAutoSyncPatchCollector autoSyncPatchCollector,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.registryCollection = registryCollection;
        this.autoSyncPatchCollector = autoSyncPatchCollector;
        this.objectManager = objectManager;
        messageBroker.Subscribe<RegisterAllGameObjects>(Handle);
        messageBroker.Subscribe<PatchLifetimes>(Handle);
        messageBroker.Subscribe<ClearAllRegistries>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RegisterAllGameObjects>(Handle);
        messageBroker.Unsubscribe<PatchLifetimes>(Handle);
        messageBroker.Unsubscribe<ClearAllRegistries>(Handle);
    }

    private void Handle(MessagePayload<PatchLifetimes> payload)
    {
        autoSyncPatchCollector.PatchAll();

        messageBroker.Publish(this, new LifetimesPatched());
    }

    private void Handle(MessagePayload<RegisterAllGameObjects> obj)
    {
        // Clear object manager to prevent stale ids
        objectManager.Clear();
        PartyExtensions.InvalidateCache();

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
