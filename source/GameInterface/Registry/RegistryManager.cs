using Common.Messaging;
using GameInterface.AutoSync;
using GameInterface.Registry.Auto;
using GameInterface.Registry.Messages;
using GameInterface.Services.ItemModifiers;
using GameInterface.Services.ItemObjects;
using GameInterface.Services.ObjectManager;

namespace GameInterface.Registry;

public interface IRegistryManager
{
    void PatchLifetimes();
    void RegisterAllGameObjects();

    /// <summary>Registers only items and item modifiers, for a session client that loads no campaign.</summary>
    void RegisterMissionItemObjects();
    void ClearAllRegistries();
}

internal class RegistryManager : IRegistryManager
{
    private readonly IObjectManager objectManager;
    private readonly IRegistryCollection registryCollection;
    private readonly IMessageBroker messageBroker;
    private readonly IAutoRegistryFactory autoRegistryFactory;
    private readonly IAutoSyncPatchCollector autoSyncPatchCollector;
    private readonly ItemObjectRegistry itemObjectRegistry;
    private readonly ItemModifierRegistry itemModifierRegistry;

    public RegistryManager(
        IObjectManager objectManager,
        IRegistryCollection registryCollection,
        IMessageBroker messageBroker,
        IAutoRegistryFactory autoRegistryFactory,
        IAutoSyncPatchCollector autoSyncPatchCollector,
        ItemObjectRegistry itemObjectRegistry,
        ItemModifierRegistry itemModifierRegistry)
    {
        this.objectManager = objectManager;
        this.registryCollection = registryCollection;
        this.messageBroker = messageBroker;
        this.autoRegistryFactory = autoRegistryFactory;
        this.autoSyncPatchCollector = autoSyncPatchCollector;
        this.itemObjectRegistry = itemObjectRegistry;
        this.itemModifierRegistry = itemModifierRegistry;
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

    public void RegisterMissionItemObjects()
    {
        itemObjectRegistry.RegisterAllObjects();
        itemModifierRegistry.RegisterAllObjects();
    }

    public void ClearAllRegistries()
    {
        registryCollection.ClearRegistries();
        objectManager.Clear();
    }
}
