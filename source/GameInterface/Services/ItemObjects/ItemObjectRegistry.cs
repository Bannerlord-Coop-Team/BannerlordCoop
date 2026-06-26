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

    public ItemObjectRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(ItemObject));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        // Must order by string id as this is not deterministic on load
        foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>().OrderBy(i => i.StringId))
        {
            RegisterExistingObject(item.StringId, item);
        }
    }

    public bool TryRegisterExistingItem(ItemObject item, out string itemId)
    {
        itemId = null;

        if (item == null)
            return false;

        if (objectManager.TryGetId(item, out itemId))
            return true;

        if (string.IsNullOrEmpty(item.StringId))
            return false;

        itemId = IdPrefix + item.StringId;
        if (objectManager.Contains(itemId))
        {
            if (objectManager.TryGetObject<ItemObject>(itemId, out var registeredItem) &&
                registeredItem != item &&
                registeredItem.StringId == item.StringId)
            {
                objectManager.Remove(registeredItem);
                if (objectManager.AddExisting(itemId, item))
                    return objectManager.TryGetId(item, out itemId);
            }

            return objectManager.TryGetId(item, out itemId);
        }

        if (objectManager.AddExisting(itemId, item) == false)
            return false;

        return objectManager.TryGetId(item, out itemId);
    }

    public bool TryGetRegisteredItem(string itemId, out ItemObject item)
    {
        if (objectManager.TryGetObject(itemId, out item))
            return true;

        var stringId = GetItemStringId(itemId);
        if (string.IsNullOrEmpty(stringId))
            return false;

        var mbObjectManager = MBObjectManager.Instance;
        if (mbObjectManager == null)
            return false;

        item = mbObjectManager.GetObject<ItemObject>(stringId) ??
               mbObjectManager.GetObjectTypeList<ItemObject>().FirstOrDefault(i => i.StringId == stringId);
        if (item == null)
            return false;

        if (TryRegisterExistingItem(item, out _) == false)
            return false;

        return objectManager.TryGetObject(itemId, out item);
    }

    private static string GetItemStringId(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return null;

        return itemId.StartsWith(IdPrefix, StringComparison.Ordinal)
            ? itemId.Substring(IdPrefix.Length)
            : itemId;
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
