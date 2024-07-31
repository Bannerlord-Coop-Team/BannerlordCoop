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

    void Build();
}
internal class AutoSyncBuilder<T> : IAutoSyncBuilder<T> where T : class
{
    private const string HarmonyId = nameof(AutoSyncBuilder<T>);

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IRegistryCollection registryCollection;

    private readonly IPatchCollection patchCollection = new PatchCollection();
    private readonly List<IDisposable> disposables = new List<IDisposable>();
    private readonly Harmony harmony = new Harmony(HarmonyId);

    public AutoSyncBuilder(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IRegistryCollection registryCollection)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.registryCollection = registryCollection;
    }

    public void Dispose()
    {
        foreach (IDisposable disposable in disposables) disposable.Dispose();

        if (Harmony.HasAnyPatches(HarmonyId) == false) return;

        harmony.UnpatchAll(HarmonyId);
    }

    public IAutoSyncBuilder<T> SyncCreation()
    {
        var lifetimeSync = new AutoCreationSync<T>(messageBroker, network, objectManager, registryCollection, patchCollection);

        disposables.Add(lifetimeSync);

        return this;
    }

    public IAutoSyncBuilder<T> SyncDeletion(MethodInfo deletionFunction)
    {
        var deletionSync = new AutoDeletionSync<T>(messageBroker, network, objectManager, patchCollection, deletionFunction);

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

    public void Build()
    {
        foreach(Patch patch in patchCollection.Patches)
        {
            var patchMethod = new HarmonyMethod(patch.PatchMethod);

            switch(patch.Type)
            {
                case PatchType.Prefix:
                    harmony.Patch(patch.TargetMethod, prefix: patchMethod);
                    break;
                case PatchType.Postfix:
                    harmony.Patch(patch.TargetMethod, postfix: patchMethod);
                    break;
                default: throw new ArgumentException($"Patch type {patch.Type} is not valid");
            }
        }
    }
}
