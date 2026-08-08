using Common.Messaging;
using GameInterface.AutoSync;
using GameInterface.Registry.Auto;
using GameInterface.Registry.Messages;
using GameInterface.Services.ObjectManager;

namespace GameInterface.Registry;

public interface IRegistryManager
{
    void PatchLifetimes();
    void RegisterAllGameObjects();
    void RegisterUntrackedGameObjects();
    void ClearAllRegistries();
}

internal class RegistryManager : IRegistryManager
{
    private readonly IObjectManager objectManager;
    private readonly IRegistryCollection registryCollection;
    private readonly IMessageBroker messageBroker;
    private readonly IAutoRegistryFactory autoRegistryFactory;
    private readonly IAutoSyncPatchCollector autoSyncPatchCollector;

    public RegistryManager(
        IObjectManager objectManager,
        IRegistryCollection registryCollection,
        IMessageBroker messageBroker,
        IAutoRegistryFactory autoRegistryFactory,
        IAutoSyncPatchCollector autoSyncPatchCollector)
    {
        this.objectManager = objectManager;
        this.registryCollection = registryCollection;
        this.messageBroker = messageBroker;
        this.autoRegistryFactory = autoRegistryFactory;
        this.autoSyncPatchCollector = autoSyncPatchCollector;
    }

    public void PatchLifetimes()
    {
        autoSyncPatchCollector.PatchAll();
    }

    public void RegisterAllGameObjects()
    {
        RegisterUntrackedGameObjects();

        messageBroker.Publish(this, new AllGameObjectsRegistered());
    }

    public void RegisterUntrackedGameObjects()
    {
        autoRegistryFactory.RegisterAll();
    }

    public void ClearAllRegistries()
    {
        registryCollection.ClearRegistries();
        objectManager.Clear();
    }
}
