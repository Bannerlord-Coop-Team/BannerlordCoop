using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Registry;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync;

public interface IAutoSyncBuilder<T> : IDisposable where T : class
{
    IAutoSyncBuilder<T> SyncCreation();
    IAutoSyncBuilder<T> SyncDeletion(MethodInfo deletionFunction);
    IAutoSyncBuilder<T> SyncField(FieldInfo field);
    IAutoSyncBuilder<T> SyncFields(IEnumerable<FieldInfo> fields);
    IAutoSyncBuilder<T> SyncProperty(PropertyInfo property);
    IAutoSyncBuilder<T> SyncPropertys(IEnumerable<PropertyInfo> properties);
}
internal class AutoSyncBuilder<T> : IAutoSyncBuilder<T> where T : class
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IRegistryCollection registryCollection;
    private readonly IAutoSyncPatcher autoSyncPatcher;

    private readonly List<IDisposable> disposables = new List<IDisposable>();

    public AutoSyncBuilder(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IRegistryCollection registryCollection,
        IAutoSyncPatcher autoSyncPatcher)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.registryCollection = registryCollection;
        this.autoSyncPatcher = autoSyncPatcher;
    }

    public void Dispose()
    {
        foreach (IDisposable disposable in disposables) disposable.Dispose();
    }

    public IAutoSyncBuilder<T> SyncCreation()
    {
        var lifetimeSync = new AutoCreationSync<T>(messageBroker, network, objectManager, registryCollection, autoSyncPatcher);

        disposables.Add(lifetimeSync);

        return this;
    }

    public IAutoSyncBuilder<T> SyncDeletion(MethodInfo deletionFunction)
    {
        var deletionSync = new AutoDeletionSync<T>(messageBroker, network, objectManager, autoSyncPatcher, deletionFunction);

        disposables.Add(deletionSync);

        return this;
    }

    public IAutoSyncBuilder<T> SyncField(FieldInfo field)
    {
        return this;
    }
    public IAutoSyncBuilder<T> SyncProperty(PropertyInfo property)
    {
        return this;
    }

    public IAutoSyncBuilder<T> SyncFields(IEnumerable<FieldInfo> fields)
    {
        foreach (FieldInfo field in fields)
        {
            SyncField(field);
        }

        return this;
    }

    public IAutoSyncBuilder<T> SyncPropertys(IEnumerable<PropertyInfo> properties)
    {
        foreach (PropertyInfo property in properties)
        {
            SyncProperty(property);
        }

        return this;
    }
}
