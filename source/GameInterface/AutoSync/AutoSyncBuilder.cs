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
    IAutoSyncBuilder<T> SyncProperty<ValueType>(PropertyInfo property);
    IAutoSyncBuilder<T> SyncPropertys(IEnumerable<PropertyInfo> properties);
}
internal class AutoSyncBuilder<T> : IAutoSyncBuilder<T> where T : class
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IRegistryCollection registryCollection;
    private readonly IAutoSyncPatcher autoSyncPatcher;
    private readonly IAutoSyncTypeMapper autoSyncTypeMapper;
    private readonly List<IDisposable> disposables = new List<IDisposable>();

    public AutoSyncBuilder(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IRegistryCollection registryCollection,
        IAutoSyncPatcher autoSyncPatcher,
        IAutoSyncTypeMapper autoSyncTypeMapper)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.registryCollection = registryCollection;
        this.autoSyncPatcher = autoSyncPatcher;
        this.autoSyncTypeMapper = autoSyncTypeMapper;
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

    public IAutoSyncBuilder<T> SyncProperty<ValueType>(PropertyInfo property)
    {
        if (property.SetMethod == null) throw new ArgumentException($"Unable to sync property with no setter: {property.Name}");

        var propSync = new AutoPropertySync<T, ValueType>(messageBroker, network, objectManager, autoSyncPatcher, autoSyncTypeMapper, property.SetMethod);

        disposables.Add(propSync);

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
            AccessTools.Method(GetType(), nameof(SyncProperty)).MakeGenericMethod(property.PropertyType).Invoke(this, new object[] { property });
        }

        return this;
    }
}
