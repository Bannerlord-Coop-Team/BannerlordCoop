using Common.Messaging;
using Common.Network;
using GameInterface.AutoSync.Internal;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Registry;
using HarmonyLib;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync;

public interface IAutoSyncBuilder<T> : IDisposable where T : class
{
    IAutoSyncBuilder<T> SyncCreation(IEnumerable<T> existingObjects = null);
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
    private readonly IAutoSyncPropertyMapper autoSyncTypeMapper;
    private readonly List<IDisposable> disposables = new List<IDisposable>();

    public AutoSyncBuilder(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IRegistryCollection registryCollection,
        IAutoSyncPatcher autoSyncPatcher,
        IAutoSyncPropertyMapper autoSyncTypeMapper)
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

    public IAutoSyncBuilder<T> SyncCreation(IEnumerable<T> existingObjects = null)
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
        if (property.SetMethod == null) throw new ArgumentException($"Unable to sync property with no setter: {property.Name}");

        object[] args = new object[] { messageBroker, network, objectManager, autoSyncPatcher, autoSyncTypeMapper, property.SetMethod };
        Type propertySyncType;

        if (RuntimeTypeModel.Default.CanSerializeBasicType(property.PropertyType))
        {
            propertySyncType = typeof(AutoPropertySync<,>).MakeGenericType(typeof(T), property.PropertyType);
        }
        else
        {
            propertySyncType = typeof(AutoPropertySyncAsRef<,>).MakeGenericType(typeof(T), property.PropertyType);
        }

        disposables.Add((IDisposable)Activator.CreateInstance(propertySyncType, args));

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
