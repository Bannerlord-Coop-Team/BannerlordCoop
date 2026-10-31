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

    /// <summary>
    /// Registers the campaign objects created since <see cref="RegisterAllGameObjects"/> without
    /// announcing <see cref="AllGameObjectsRegistered"/> again, whose handlers run once per load.
    /// Objects that already have an id are skipped.
    /// </summary>
    void RegisterNewGameObjects();

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
        autoRegistryFactory.RegisterAll();

        messageBroker.Publish(this, new AllGameObjectsRegistered());
    }

    public void RegisterNewGameObjects()
    {
        autoRegistryFactory.RegisterAll();
    }

    public void ClearAllRegistries()
    {
        registryCollection.ClearRegistries();
        objectManager.Clear();
    }
}
