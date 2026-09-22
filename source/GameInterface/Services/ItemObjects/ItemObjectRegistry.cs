using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.ItemObjects;

public class ItemObjectRegistry : AutoRegistryBase<ItemObject>
{
    private const string IdPrefix = nameof(ItemObject) + "_";
    private readonly HashSet<uint> handlesKnownToClients = new();

    public ItemObjectRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(ItemObject));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        if (!IsCollectingIdRemap)
            handlesKnownToClients.Clear();

        // Must order by string id as this is not deterministic on load
        foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>().OrderBy(i => i.StringId))
        {
            RegisterExistingObject(item.StringId, item);
            if (!IsCollectingIdRemap && objectManager.TryGetHandle(item, out var handle))
                handlesKnownToClients.Add(handle);
        }
    }

    public bool TryRegisterExistingItem(
        ItemObject item,
        out string itemId,
        out uint itemHandle,
        out bool announceHandle)
    {
        itemId = null;
        itemHandle = 0;
        announceHandle = false;

        if (item == null)
            return false;

        if (objectManager.TryGetId(item, out itemId))
        {
            if (!objectManager.TryGetHandle(item, out itemHandle))
                return false;

            announceHandle = !handlesKnownToClients.Contains(itemHandle);
            return true;
        }

        if (string.IsNullOrEmpty(item.StringId))
            return false;

        itemId = IdPrefix + item.StringId;
        if (objectManager.Contains(itemId))
        {
            if (objectManager.TryGetObject<ItemObject>(itemId, out var registeredItem) &&
                registeredItem != item &&
                registeredItem.StringId == item.StringId)
            {
                if (!objectManager.TryGetHandle(registeredItem, out itemHandle))
                    return false;

                if (!objectManager.Remove(registeredItem))
                    return false;

                if (objectManager.AddExisting(itemId, item, itemHandle))
                {
                    announceHandle = !handlesKnownToClients.Contains(itemHandle);
                    return true;
                }
            }

            if (!objectManager.TryGetId(item, out itemId) ||
                !objectManager.TryGetHandle(item, out itemHandle))
            {
                return false;
            }

            announceHandle = !handlesKnownToClients.Contains(itemHandle);
            return true;
        }

        if (!objectManager.AddExisting(itemId, item))
            return false;

        if (!objectManager.TryGetHandle(item, out itemHandle))
            return false;

        announceHandle = !handlesKnownToClients.Contains(itemHandle);
        return true;
    }

    public void MarkHandleKnownToClients(uint itemHandle)
    {
        if (itemHandle != 0)
            handlesKnownToClients.Add(itemHandle);
    }

    public bool TryRegisterExistingItem(string stringId, uint itemHandle)
    {
        if (string.IsNullOrEmpty(stringId) || itemHandle == 0)
            return false;

        var mbObjectManager = MBObjectManager.Instance;
        var item = mbObjectManager?.GetObject<ItemObject>(stringId) ??
                   mbObjectManager?.GetObjectTypeList<ItemObject>().FirstOrDefault(value => value.StringId == stringId);
        if (item == null)
        {
            Logger.Error("Failed to register item handle {Handle}, item {StringId} was not found", itemHandle, stringId);
            return false;
        }

        var itemId = IdPrefix + stringId;
        if (objectManager.TryGetId(item, out _) &&
            objectManager.TryGetHandle(item, out var existingHandle) &&
            existingHandle == itemHandle)
        {
            handlesKnownToClients.Add(itemHandle);
            return true;
        }

        if (objectManager.TryGetObject<ItemObject>(itemId, out var registeredItem))
            objectManager.Remove(registeredItem);

        if (!objectManager.AddExisting(itemId, item, itemHandle))
            return false;

        handlesKnownToClients.Add(itemHandle);
        return true;
    }

    public override void OnClientCreated(ItemObject obj, string id)
    {
    }

    public override void OnClientDestroyed(ItemObject obj, string id)
    {
    }

    public override void OnServerCreated(ItemObject obj, string id)
    {
    }

    public override void OnServerDestroyed(ItemObject obj, string id)
    {
    }
}
